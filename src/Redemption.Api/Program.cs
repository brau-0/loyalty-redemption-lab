using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Redemption.Api;
using Redemption.Infrastructure;
using Redemption.Infrastructure.Messaging;
using System.Text.Json.Serialization;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<RedemptionDbContext>("redemptiondb");
builder.AddAzureServiceBusClient("messaging");
builder.AddRedisClient("cache");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ServiceBusSender is thread-safe: one shared instance per topic.
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<ServiceBusClient>().CreateSender(Topics.RedemptionEvents));

// Service discovery resolves the mocks; ServiceDefaults adds the standard resilience handler.
builder.Services.AddHttpClient<CustomersClient>(c => c.BaseAddress = new Uri("https+http://mock-customers"));
builder.Services.AddHttpClient<PointsBalanceClient>(c => c.BaseAddress = new Uri("https+http://mock-points"));
builder.Services.AddScoped<RedemptionEligibility>();

var app = builder.Build();
app.MapDefaultEndpoints();

// Development only: apply migrations at startup. In production this runs as a pipeline step.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<RedemptionDbContext>().Database.MigrateAsync();
}

app.MapRedemptionEndpoints();
app.MapDeliveryWebhook();
if (app.Environment.IsDevelopment())
    app.MapDeliverySimulatorEndpoints();

app.Run();