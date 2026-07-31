using System.Net;

namespace TenderScope.Infrastructure.Crawling;

public static class CrawlerPolicy
{
    public const int MaxResponseBytes = 10 * 1024 * 1024;

    public static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Clamp(attempt, 1, 6)), 60));

    public static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
