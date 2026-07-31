using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api;

public sealed record IngestionSourceResult(string Key, int Imported, bool Success, string? Error);
public sealed record IngestionReport(DateTimeOffset StartedAt, DateTimeOffset CompletedAt, int Imported, IReadOnlyList<IngestionSourceResult> Sources);

public sealed class TenderIngestionService(
    IEnumerable<ITenderSource> collectors,
    ITenderRepository tenders,
    ITenderSourceRepository sourceRepository,
    OperationalMetrics metrics,
    ILogger<TenderIngestionService> logger)
{
    public async Task<IngestionReport> RunAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<IngestionSourceResult>();
        var total = 0;

        foreach (var collector in collectors)
        {
            var registeredSource = await sourceRepository.FindByKeyAsync(collector.Key, cancellationToken);
            if (registeredSource is { IsEnabled: false }) continue;

            var imported = 0;
            try
            {
                await foreach (var tender in collector.FetchAsync(cancellationToken))
                {
                    await tenders.UpsertAsync(tender, cancellationToken);
                    imported++;
                }

                await tenders.SaveChangesAsync(cancellationToken);
                registeredSource?.MarkSucceeded(DateTimeOffset.UtcNow);
                await sourceRepository.SaveChangesAsync(cancellationToken);
                total += imported;
                results.Add(new IngestionSourceResult(collector.Key, imported, true, null));
                logger.LogInformation("Ingestion source {SourceKey} completed with {Imported} records", collector.Key, imported);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                registeredSource?.MarkFailed(DateTimeOffset.UtcNow, exception.Message);
                await sourceRepository.SaveChangesAsync(cancellationToken);
                results.Add(new IngestionSourceResult(collector.Key, imported, false, exception.Message));
                logger.LogError(exception, "Ingestion source {SourceKey} failed", collector.Key);
            }
        }

        metrics.RecordIngestion(total, results.All(x => x.Success));
        return new IngestionReport(startedAt, DateTimeOffset.UtcNow, total, results);
    }
}

public sealed class ScheduledIngestionWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduledIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var runId = Guid.NewGuid();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
                await using var jobLock = scope.ServiceProvider.GetRequiredService<DistributedJobLock>();
                if (!await jobLock.TryAcquireAsync("tenderscope:ingestion", stoppingToken))
                {
                    logger.LogInformation("Scheduled ingestion skipped because another instance owns the distributed lock");
                }
                else
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO operational_job_runs (\"Id\", \"JobName\", \"InstanceId\", \"StartedAt\", \"Attempt\") VALUES ({runId}, {"tenderscope:ingestion"}, {Environment.MachineName}, {DateTimeOffset.UtcNow}, {1})", stoppingToken);
                    var ingestion = scope.ServiceProvider.GetRequiredService<TenderIngestionService>();
                    var report = await ingestion.RunAsync(stoppingToken);
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_job_runs SET \"CompletedAt\"={DateTimeOffset.UtcNow}, \"Succeeded\"={true}, \"RecordsAffected\"={report.Imported} WHERE \"Id\"={runId}", stoppingToken);
                    logger.LogInformation("Scheduled ingestion completed with {Imported} imported records", report.Imported);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var failureScope = scopeFactory.CreateAsyncScope();
                    var db = failureScope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_job_runs SET \"CompletedAt\"={DateTimeOffset.UtcNow}, \"Succeeded\"={false}, \"Error\"={exception.Message[..Math.Min(exception.Message.Length, 4000)]} WHERE \"Id\"={runId}", stoppingToken);
                }
                catch { }
                logger.LogError(exception, "Scheduled ingestion cycle failed");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
