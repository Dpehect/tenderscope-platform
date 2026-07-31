using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenderScope.Application.Contracts;
using TenderScope.Infrastructure.Normalization;
using TenderScope.Infrastructure.Parsing;
using TenderScope.Infrastructure.Persistence;
using TenderScope.Infrastructure.Sources;

namespace TenderScope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TenderScopeDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres"), npgsql => npgsql.EnableRetryOnFailure(5)));

        services.AddScoped<ITenderRepository, TenderRepository>();
        services.AddScoped<ITenderSourceRepository, TenderSourceRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddSingleton<IDuplicateDetector, DuplicateDetector>();
        services.AddScoped<ITenderNormalizer, TenderNormalizer>();
        services.AddSingleton<ITenderParser, JsonTenderParser>();
        services.AddSingleton<ITenderParser, XmlTenderParser>();

        services.AddHttpClient("crawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TenderScopeBot/1.0 (+https://github.com/Dpehect/tenderscope-platform)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, application/xml, text/xml, text/html;q=0.9, */*;q=0.5");
        });
        services.AddHttpClient<TedSearchTenderSource>(client =>
        {
            client.BaseAddress = new Uri("https://api.ted.europa.eu/");
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TenderScopeBot/1.0");
        });

        services.AddSingleton<ITenderSource, DemoTenderSource>();
        services.AddScoped<ITenderSource>(provider => provider.GetRequiredService<TedSearchTenderSource>());
        return services;
    }
}
