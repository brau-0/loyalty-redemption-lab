using Redemption.Infrastructure;
using Redemption.Worker;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<RedemptionDbContext>("redemptiondb");
builder.AddAzureServiceBusClient("messaging");
builder.Services.AddHostedService<OutboxRelay>();
builder.Services.AddHostedService<AuditConsumer>();
builder.Build().Run();