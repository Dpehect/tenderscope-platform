using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenderScope.Application.Contracts;
using TenderScope.Infrastructure.Persistence;
using TenderScope.Infrastructure.Sources;

namespace TenderScope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TenderScopeDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<ITenderRepository, TenderRepository>();
        services.AddSingleton<ITenderSource, DemoTenderSource>();
        return services;
    }
}
