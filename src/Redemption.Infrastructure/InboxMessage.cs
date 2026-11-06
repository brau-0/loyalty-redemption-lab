namespace Redemption.Infrastructure;

/// <summary>Records messages already handled by a consumer, so redeliveries have no side effects.</summary>
public sealed class InboxMessage
{
    public string MessageId { get; set; } = default!;
    public string Consumer { get; set; } = default!;
    public DateTimeOffset ProcessedAt { get; set; }
}