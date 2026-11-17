using Azure.Messaging.ServiceBus;
using Redemption.Domain;
using Redemption.Infrastructure;
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
        app.MapGet("/dev/dead-letters", async (string? subscription, ServiceBusClient client, CancellationToken ct) =>
        {
            await using var receiver = client.CreateReceiver(Topics.RedemptionEvents, subscription ?? "orchestrator",
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

        // Bulk-creates finished redemptions for load testing. Events are cleared before saving,
        // so no outbox messages, shipments or reservations are produced.
        app.MapPost("/dev/seed-redemptions", async (
            string customerId, int count, RedemptionDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var start = clock.GetUtcNow().AddDays(-30);
            for (var i = 0; i < count; i++)
            {
                var at = start.AddMinutes(i * 10);
                var order = RedemptionOrder.Create(Guid.CreateVersion7(), customerId,
                    [new RedemptionLine("CAP-01", 1 + i % 3, 500)], at);

                order.ApplyDeliveryStatus(DeliveryStatus.Pending, at);
                if (i % 5 == 0)
                {
                    order.ApplyDeliveryStatus(DeliveryStatus.NotCompleted, at);   // ~20% cancelled
                }
                else
                {
                    order.ApplyDeliveryStatus(DeliveryStatus.Held, at);
                    order.ConfirmPointsCommitted(at);
                    order.ApplyDeliveryStatus(DeliveryStatus.OnDelivery, at);
                    order.ApplyDeliveryStatus(DeliveryStatus.Completed, at);
                }

                order.ClearDomainEvents();
                db.Redemptions.Add(order);

                if (i % 500 == 499)
                {
                    await db.SaveChangesAsync(ct);
                    db.ChangeTracker.Clear();
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { customerId, created = count });
        });


    }
}