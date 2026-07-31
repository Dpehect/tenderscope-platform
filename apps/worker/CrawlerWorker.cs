using TenderScope.Application.Contracts;

namespace TenderScope.Worker;

public sealed class CrawlerWorker(IServiceScopeFactory scopeFactory, ILogger<CrawlerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITenderRepository>();
                var sources = scope.ServiceProvider.GetServices<ITenderSource>();
                foreach (var source in sources)
                {
                    await foreach (var tender in source.FetchAsync(stoppingToken))
                        await repository.UpsertAsync(tender, stoppingToken);
                }
                await repository.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Tender ingestion cycle completed at {UtcNow}", DateTimeOffset.UtcNow);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Tender ingestion cycle failed");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
