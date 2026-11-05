using Azure.Messaging.ServiceBus;
using Redemption.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.Services.AddHostedService<SmokeConsumer>();
builder.Build().Run();