using Azure.Messaging.ServiceBus;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Api;

// Development-only: simulates the delivery provider's webhook by publishing a status change.
// Lab 4 replaces it with an Azure Function that receives the provider's real webhook.
public static class DeliverySimulatorEndpoints
{
    public static void MapDeliverySimulatorEndpoints(this IEndpointRouteBuilder app)
    {
        // Optional messageId lets you resend the exact same message to test idempotency.
        app.MapPost("/dev/redemptions/{id:guid}/delivery-status", async (
            Guid id, DeliveryStatusRequest request, string? messageId,
            ServiceBusClient client, TimeProvider clock, CancellationToken ct) =>
        {
            await using var sender = client.CreateSender(Topics.RedemptionEvents);
            var change = new DeliveryStatusChanged(id, request.Status, request.Delivered, clock.GetUtcNow());
            await sender.SendMessageAsync(MessageFactory.Create(change, id, messageId), ct);
            return Results.Accepted($"/redemptions/{id}");
        });

        // Peeks the orchestrator's dead-letter queue (the emulator has no portal).
        app.MapGet("/dev/dead-letters", async (ServiceBusClient client, CancellationToken ct) =>
        {
            await using var receiver = client.CreateReceiver(Topics.RedemptionEvents, "orchestrator",
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
            var messages = await receiver.PeekMessagesAsync(maxMessages: 50, cancellationToken: ct);
            return Results.Ok(messages.Select(m => new
            {
                m.MessageId,
                m.SessionId,
                m.Subject,
                m.DeadLetterReason,
                m.DeadLetterErrorDescription,
                Body = m.Body.ToString()
            }));
        });
    }
}