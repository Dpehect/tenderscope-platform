using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api;

public sealed record IngestionSourceResult(string Key, int Imported, bool Success, string? Error, int Attempts = 1, Guid? RunId = null);
public sealed record IngestionReport(DateTimeOffset StartedAt, DateTimeOffset CompletedAt, int Imported, IReadOnlyList<IngestionSourceResult> Sources);

public sealed class TenderIngestionService(
    IEnumerable<ITenderSource> collectors,
    ITenderRepository tenders,
    ITenderSourceRepository sourceRepository,
    TenderScopeDbContext db,
    OperationalMetrics metrics,
    ILogger<TenderIngestionService> logger)
{
    private const int MaxAttempts = 3;

    public async Task<IngestionReport> RunAsync(CancellationToken cancellationToken, string? sourceKey = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<IngestionSourceResult>();
        var total = 0;

        foreach (var collector in collectors.Where(x => sourceKey is null || x.Key.Equals(sourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            var registeredSource = await sourceRepository.FindByKeyAsync(collector.Key, cancellationToken);
            if (registeredSource is null || !registeredSource.IsEnabled) continue;
            if (registeredSource.NextCrawlAt > DateTimeOffset.UtcNow && sourceKey is null) continue;

            IngestionSourceResult? result = null;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var runId = Guid.NewGuid();
                var stopwatch = Stopwatch.StartNew();
                var imported = 0;
                var fetched = 0;
                await InsertRunAsync(runId, registeredSource.Id, collector.Key, registeredSource.ParserVersion, attempt, cancellationToken);

                try
                {
                    await foreach (var tender in collector.FetchAsync(cancellationToken))
                    {
                        fetched++;
                        await tenders.UpsertAsync(tender, cancellationToken);
                        imported++;
                    }

                    await tenders.SaveChangesAsync(cancellationToken);
                    registeredSource.MarkSucceeded(DateTimeOffset.UtcNow, imported);
                    await sourceRepository.SaveChangesAsync(cancellationToken);
                    stopwatch.Stop();
                    await CompleteRunAsync(runId, fetched, imported, 0, stopwatch.Elapsed, cancellationToken);
                    total += imported;
                    result = new IngestionSourceResult(collector.Key, imported, true, null, attempt, runId);
                    logger.LogInformation("Ingestion source {SourceKey} completed with {Imported} records in {Attempts} attempt(s)", collector.Key, imported, attempt);
                    break;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    await FailRunAsync(runId, exception, stopwatch.Elapsed, cancellationToken);
                    logger.LogWarning(exception, "Ingestion source {SourceKey} attempt {Attempt}/{MaxAttempts} failed", collector.Key, attempt, MaxAttempts);

                    if (attempt < MaxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                        continue;
                    }

                    registeredSource.MarkFailed(DateTimeOffset.UtcNow, exception.Message);
                    await sourceRepository.SaveChangesAsync(cancellationToken);
                    await InsertDeadLetterAsync(runId, registeredSource.Id, collector.Key, exception, attempt, cancellationToken);
                    result = new IngestionSourceResult(collector.Key, imported, false, exception.Message, attempt, runId);
                }
            }

            if (result is not null) results.Add(result);
        }

        metrics.RecordIngestion(total, results.Count > 0 && results.All(x => x.Success));
        return new IngestionReport(startedAt, DateTimeOffset.UtcNow, total, results);
    }

    private Task InsertRunAsync(Guid id, Guid sourceId, string key, string parserVersion, int attempt, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO crawl_runs (\"Id\", \"SourceId\", \"SourceKey\", \"ParserVersion\", \"Status\", \"Attempt\", \"StartedAt\") VALUES ({id}, {sourceId}, {key}, {parserVersion}, {0}, {attempt}, {DateTimeOffset.UtcNow})", ct);

    private Task CompleteRunAsync(Guid id, int fetched, int imported, int rejected, TimeSpan duration, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crawl_runs SET \"Status\"={rejected > 0 ? 2 : 1}, \"FetchedCount\"={fetched}, \"ImportedCount\"={imported}, \"RejectedCount\"={rejected}, \"DurationMilliseconds\"={(long)duration.TotalMilliseconds}, \"CompletedAt\"={DateTimeOffset.UtcNow} WHERE \"Id\"={id}", ct);

    private Task FailRunAsync(Guid id, Exception exception, TimeSpan duration, CancellationToken ct)
    {
        var error = exception.Message[..Math.Min(exception.Message.Length, 4000)];
        return db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crawl_runs SET \"Status\"={3}, \"Error\"={error}, \"DurationMilliseconds\"={(long)duration.TotalMilliseconds}, \"CompletedAt\"={DateTimeOffset.UtcNow} WHERE \"Id\"={id}", ct);
    }

    private Task InsertDeadLetterAsync(Guid runId, Guid sourceId, string key, Exception exception, int attempts, CancellationToken ct)
    {
        var error = exception.ToString()[..Math.Min(exception.ToString().Length, 4000)];
        return db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO crawl_dead_letters (\"Id\", \"CrawlRunId\", \"SourceId\", \"SourceKey\", \"Error\", \"Attempts\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {runId}, {sourceId}, {key}, {error}, {attempts}, {DateTimeOffset.UtcNow})", ct);
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

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
