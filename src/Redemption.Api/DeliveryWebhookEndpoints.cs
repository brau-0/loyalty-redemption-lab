using Azure.Messaging.ServiceBus;
using Redemption.Domain;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Api;

public sealed record DeliveryWebhook(
    string EventId, string Reference, string Status, List<DeliveryWebhookItem>? DeliveredItems, DateTimeOffset OccurredAt);
public sealed record DeliveryWebhookItem(string Sku, int Quantity);

/// <summary>
/// Receives the delivery provider's webhooks and translates them into our domain language
/// (anti-corruption layer). Lab 4 moves this endpoint to an Azure Function.
/// </summary>
public static class DeliveryWebhookEndpoints
{
    public static void MapDeliveryWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/delivery", async (DeliveryWebhook webhook, ServiceBusSender sender, CancellationToken ct) =>
        {
            if (!Guid.TryParse(webhook.Reference, out var redemptionId))
                return Results.BadRequest(new { error = $"Unknown reference '{webhook.Reference}'." });

            if (MapStatus(webhook.Status) is not { } status)
                return Results.BadRequest(new { error = $"Unknown status '{webhook.Status}'." });

            var delivered = webhook.DeliveredItems?.Select(i => new DeliveredLine(i.Sku, i.Quantity)).ToList();
            var change = new DeliveryStatusChanged(redemptionId, status, delivered, webhook.OccurredAt);

            // The provider's event id becomes the MessageId, so provider retries are deduplicated by the inbox.
            await sender.SendMessageAsync(MessageFactory.Create(change, redemptionId, $"delivery-{webhook.EventId}"), ct);
            return Results.Accepted();
        });
    }

    // The provider's vocabulary never leaks past this method.
    private static DeliveryStatus? MapStatus(string providerStatus) => providerStatus.ToLowerInvariant() switch
    {
        "pending" => DeliveryStatus.Pending,
        "holded" => DeliveryStatus.Held,
        "ondelivery" => DeliveryStatus.OnDelivery,
        "completed" => DeliveryStatus.Completed,
        "completed_partially" => DeliveryStatus.CompletedPartially,
        "not_completed" => DeliveryStatus.NotCompleted,
        _ => null
    };
}