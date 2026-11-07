namespace Redemption.Domain;

/// <summary>Something that happened in the domain and must be published (via the outbox).</summary>
public interface IDomainEvent
{
    Guid RedemptionId { get; }
    DateTimeOffset OccurredAt { get; }
}

public sealed record PointsReserved(Guid RedemptionId, string CustomerId, int Points, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PointsCommitRequested(Guid RedemptionId, string CustomerId, int Points, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ReservationReleased(Guid RedemptionId, string CustomerId, int Points, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PointsReversalRequested(Guid RedemptionId, string CustomerId, int Points, bool IsPartial, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RedemptionFinished(Guid RedemptionId, RedemptionStatus FinalStatus, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DeliveryCancellationRequested(Guid RedemptionId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RedemptionRequested(Guid RedemptionId, string CustomerId, IReadOnlyList<RedemptionLine> Lines, DateTimeOffset OccurredAt) : IDomainEvent;