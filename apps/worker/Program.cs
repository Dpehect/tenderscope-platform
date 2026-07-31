using TenderScope.Infrastructure;
using TenderScope.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<CrawlerWorker>();
await builder.Build().RunAsync();
