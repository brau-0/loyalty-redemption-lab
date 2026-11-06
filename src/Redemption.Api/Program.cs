using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Redemption.Api;
using Redemption.Infrastructure;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<RedemptionDbContext>("redemptiondb");
builder.AddAzureServiceBusClient("messaging");
builder.AddRedisClient("cache");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.MapDefaultEndpoints();

// Development only: apply migrations at startup. In production this runs as a pipeline step.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<RedemptionDbContext>().Database.MigrateAsync();
}

app.MapRedemptionEndpoints();
if (app.Environment.IsDevelopment())
    app.MapDeliverySimulatorEndpoints();

app.Run();