using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Redemption.Domain;

namespace Redemption.Infrastructure.Messaging;

public static class Topics
{
    public const string RedemptionEvents = "redemption-events";
}

// Inbound signals for the orchestrator. The Subject is always the type name.
public sealed record DeliveryStatusChanged(
    Guid RedemptionId, DeliveryStatus Status, List<DeliveredLine>? Delivered, DateTimeOffset OccurredAt);
public sealed record PointsCommitConfirmed(Guid RedemptionId, DateTimeOffset OccurredAt);
public sealed record PointsReversalConfirmed(Guid RedemptionId, DateTimeOffset OccurredAt);

public static class MessageFactory
{
    /// <summary>Builds a message routed by type (Subject) and ordered by redemption (SessionId).</summary>
    public static ServiceBusMessage Create(object payload, Guid redemptionId, string? messageId = null) =>
        new(BinaryData.FromString(JsonSerializer.Serialize(payload, payload.GetType(), OutboxJson.Options)))
        {
            MessageId = messageId ?? Guid.CreateVersion7().ToString(),
            SessionId = redemptionId.ToString(),
            Subject = payload.GetType().Name,
            ContentType = "application/json"
        };
}