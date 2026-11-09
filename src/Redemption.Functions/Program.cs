using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Redemption.Functions;
using Redemption.Infrastructure;
using Redemption.Infrastructure.Messaging;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = FunctionsApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.ConfigureFunctionsWebApplication();

builder.AddSqlServerDbContext<RedemptionDbContext>("redemptiondb");
builder.AddAzureServiceBusClient("messaging");
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<ServiceBusClient>().CreateSender(Topics.RedemptionEvents));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<DeliveryStatusClient>(c => c.BaseAddress = new Uri("https+http://mock-delivery"));

builder.Build().Run();