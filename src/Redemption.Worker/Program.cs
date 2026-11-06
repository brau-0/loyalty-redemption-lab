using Redemption.Infrastructure;
using Redemption.Worker;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<RedemptionDbContext>("redemptiondb");
// Service discovery resolves "mock-points"; ServiceDefaults adds the standard resilience handler.
builder.Services.AddHttpClient<PointsProviderClient>(c => c.BaseAddress = new Uri("https+http://mock-points"));
builder.AddAzureServiceBusClient("messaging");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<OutboxRelay>();
builder.Services.AddHostedService<AuditConsumer>();
builder.Services.AddHostedService<RedemptionOrchestrator>();
builder.Services.AddHostedService<PointsAdapter>();
builder.Build().Run();