using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api;

public sealed class DistributedJobLock(TenderScopeDbContext db) : IAsyncDisposable
{
    private long? _key;

    public async Task<bool> TryAcquireAsync(string jobName, CancellationToken cancellationToken)
    {
        var key = StableKey(jobName);
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = key; command.Parameters.Add(parameter);
        var acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (acquired) _key = key;
        return acquired;
    }

    public async ValueTask DisposeAsync()
    {
        if (_key is null) return;
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@key)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = _key.Value; command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync();
        _key = null;
    }

    private static long StableKey(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037;
            const ulong prime = 1099511628211;
            ulong hash = offset;
            foreach (var character in value) { hash ^= character; hash *= prime; }
            return (long)hash;
        }
    }
}

public sealed class OperationalMetrics
{
    private long _requests;
    private long _errors;
    private long _ingestionRuns;
    private long _ingestionFailures;
    private long _ingestedRecords;
    private readonly ConcurrentDictionary<string, long> _statusCodes = new();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public void RecordRequest(int statusCode)
    {
        Interlocked.Increment(ref _requests);
        if (statusCode >= 500) Interlocked.Increment(ref _errors);
        _statusCodes.AddOrUpdate(statusCode.ToString(), 1, (_, count) => count + 1);
    }

    public void RecordIngestion(int records, bool success)
    {
        Interlocked.Increment(ref _ingestionRuns);
        if (!success) Interlocked.Increment(ref _ingestionFailures);
        Interlocked.Add(ref _ingestedRecords, records);
    }

    public object Snapshot() => new
    {
        startedAt = _startedAt,
        uptimeSeconds = (long)(DateTimeOffset.UtcNow - _startedAt).TotalSeconds,
        requests = Interlocked.Read(ref _requests),
        serverErrors = Interlocked.Read(ref _errors),
        ingestionRuns = Interlocked.Read(ref _ingestionRuns),
        ingestionFailures = Interlocked.Read(ref _ingestionFailures),
        ingestedRecords = Interlocked.Read(ref _ingestedRecords),
        statusCodes = _statusCodes.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
    };
}

public static class ProductionOperationsExtensions
{
    public static IServiceCollection AddProductionOperations(this IServiceCollection services)
    {
        services.AddScoped<DistributedJobLock>();
        services.AddSingleton<OperationalMetrics>();
        return services;
    }

    public static IApplicationBuilder UseOperationalMetrics(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var metrics = context.RequestServices.GetRequiredService<OperationalMetrics>();
        try { await next(); }
        finally { metrics.RecordRequest(context.Response.StatusCode); }
    });

    public static IEndpointRouteBuilder MapOperationalMetrics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/operations/metrics", (OperationalMetrics metrics) => Results.Ok(metrics.Snapshot()))
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Owner"));
        return endpoints;
    }
}
