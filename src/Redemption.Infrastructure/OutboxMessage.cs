using System.Text.Json;
using System.Text.Json.Serialization;

namespace Redemption.Infrastructure;

/// <summary>Domain event stored in the same transaction as the aggregate and published later by the relay.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }                   // Becomes the Service Bus MessageId (deduplication).
    public long Sequence { get; set; }             // Identity column: guarantees publishing order.
    public Guid AggregateId { get; set; }          // Becomes the SessionId: ordering per redemption.
    public string Type { get; set; } = default!;   // Becomes the Subject: drives subscription routing.
    public string Payload { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}