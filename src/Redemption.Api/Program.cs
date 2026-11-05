using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAzureServiceBusClient("messaging");
builder.AddRedisClient("cache");

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapPost("/smoke/{redemptionId}", async (string redemptionId, ServiceBusClient client) =>
{
    await using var sender = client.CreateSender("redemption-events");
    await sender.SendMessageAsync(new ServiceBusMessage($"ping {redemptionId}")
    {
        SessionId = redemptionId,                 
        MessageId = Guid.NewGuid().ToString()
    });
    return Results.Accepted();
});

app.Run();