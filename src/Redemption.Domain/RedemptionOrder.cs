namespace Redemption.Domain;

/// <summary>
/// Redemption aggregate. Orchestrated saga state machine driven by delivery
/// status changes and points-provider confirmations.
/// </summary>
public sealed class RedemptionOrder
{
    private readonly List<RedemptionLine> _lines = [];
    private readonly List<StatusChange> _history = [];
    private readonly List<IDomainEvent> _events = [];

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = default!;
    public RedemptionStatus Status { get; private set; }
    public DeliveryStatus? LastDeliveryStatus { get; private set; }
    public int TotalPoints { get; private set; }
    public int PointsToReverse { get; private set; }
    public RedemptionStatus? StatusAfterReversal { get; private set; }
    public string? RejectionReason { get; private set; }

    public IReadOnlyList<RedemptionLine> Lines => _lines;
    public IReadOnlyList<StatusChange> History => _history;
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;
    public DateTimeOffset UpdatedAt { get; private set; }

    private RedemptionOrder() { } // Required by EF Core.

    public static RedemptionOrder Create(Guid id, string customerId, IEnumerable<RedemptionLine> lines, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentNullException.ThrowIfNull(lines);

        var items = lines.ToList();
        if (items.Count == 0)
            throw new ArgumentException("A redemption needs at least one line.", nameof(lines));
        if (items.Any(l => l.Quantity <= 0 || l.PointsPerUnit <= 0))
            throw new ArgumentException("Quantities and points must be positive.", nameof(lines));

        var order = new RedemptionOrder
        {
            Id = id,
            CustomerId = customerId,
            Status = RedemptionStatus.Created,
            TotalPoints = items.Sum(l => l.TotalPoints),
            UpdatedAt = now
        };
        order._lines.AddRange(items);
        order._history.Add(new StatusChange(null, RedemptionStatus.Created, "Created", now));
        order.Raise(new RedemptionRequested(id, customerId, items, now));
        return order;
    }

    /// <summary>Applies a delivery status change reported by the delivery provider.</summary>
    /// <summary>Applies a delivery status change reported by the delivery provider.</summary>
    public TransitionResult ApplyDeliveryStatus(
        DeliveryStatus delivery, DateTimeOffset at, IReadOnlyCollection<DeliveredLine>? delivered = null)
    {
        // Providers may resend the same status: safe no-op.
        if (delivery == LastDeliveryStatus)
            return TransitionResult.IgnoredDuplicate;

        // An older status arriving late is old news: the redemption already moved past it.
        if (LastDeliveryStatus is { } last && DeliveryStatusOrder.Rank(delivery) < DeliveryStatusOrder.Rank(last))
            return TransitionResult.IgnoredDuplicate;

        // A different signal after the redemption finished is a conflict to investigate.
        if (IsFinal(Status))
            return TransitionResult.RejectedInvalid;

        var result = (Status, delivery) switch
        {
            (RedemptionStatus.Created, DeliveryStatus.Pending) => ReservePoints(at),
            (RedemptionStatus.PointsReserved, DeliveryStatus.Held) => RequestCommit(at),
            (RedemptionStatus.PointsCommitted, DeliveryStatus.OnDelivery) =>
                Move(RedemptionStatus.InDelivery, "Delivery started", at),
            (RedemptionStatus.PointsCommitted or RedemptionStatus.InDelivery, DeliveryStatus.Completed) =>
                Finish(RedemptionStatus.Completed, "Delivery completed", at),
            (RedemptionStatus.PointsCommitted or RedemptionStatus.InDelivery, DeliveryStatus.CompletedPartially) =>
                RequestPartialReversal(delivered, at),

            // Compensation depends on how far the redemption progressed.
            (RedemptionStatus.Created, DeliveryStatus.NotCompleted) =>
                Finish(RedemptionStatus.Cancelled, "Delivery not completed before reservation", at),
            (RedemptionStatus.PointsReserved, DeliveryStatus.NotCompleted) => ReleaseReservation(at),
            (RedemptionStatus.CommitPending or RedemptionStatus.PointsCommitted or RedemptionStatus.InDelivery,
                DeliveryStatus.NotCompleted) => RequestFullReversal(at),

            // No valid transition yet: the signal arrived ahead of its predecessor.
            _ => TransitionResult.NotYetApplicable
        };

        if (result == TransitionResult.Applied)
            LastDeliveryStatus = delivery;

        return result;
    }


    private static bool IsFinal(RedemptionStatus status) =>
        status is RedemptionStatus.Completed or RedemptionStatus.PartiallyCompleted
            or RedemptionStatus.Cancelled or RedemptionStatus.Rejected;

    /// <summary>The points provider confirmed the deduction.</summary>
    public TransitionResult ConfirmPointsCommitted(DateTimeOffset at) => Status switch
    {
        RedemptionStatus.CommitPending => Move(RedemptionStatus.PointsCommitted, "Points provider confirmed deduction", at),
        // Late or repeated confirmation after the saga moved on: safe no-op.
        RedemptionStatus.PointsCommitted or RedemptionStatus.InDelivery or RedemptionStatus.ReversalPending
            or RedemptionStatus.Completed or RedemptionStatus.PartiallyCompleted or RedemptionStatus.Cancelled
            => TransitionResult.IgnoredDuplicate,
        _ => TransitionResult.RejectedInvalid
    };

    /// <summary>The points provider refused the deduction (e.g. insufficient balance).</summary>
    public TransitionResult RejectPointsCommit(string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != RedemptionStatus.CommitPending)
            return Status == RedemptionStatus.Cancelled ? TransitionResult.IgnoredDuplicate : TransitionResult.RejectedInvalid;

        RejectionReason = reason;
        // Nothing was deducted: releasing the local hold is enough, but the shipment must be stopped.
        Raise(new ReservationReleased(Id, CustomerId, TotalPoints, at));
        Raise(new DeliveryCancellationRequested(Id, reason, at));
        return Finish(RedemptionStatus.Cancelled, $"Points provider rejected deduction: {reason}", at);
    }

    /// <summary>The points provider confirmed a full or partial reversal.</summary>
    public TransitionResult ConfirmReversal(DateTimeOffset at)
    {
        if (Status == RedemptionStatus.ReversalPending)
            return Finish(StatusAfterReversal!.Value, "Points provider confirmed reversal", at);

        return Status is RedemptionStatus.Cancelled or RedemptionStatus.PartiallyCompleted
            ? TransitionResult.IgnoredDuplicate
            : TransitionResult.RejectedInvalid;
    }

    /// <summary>Business rejection before any points were touched (e.g. insufficient balance).</summary>
    public TransitionResult Reject(string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != RedemptionStatus.Created)
            return TransitionResult.RejectedInvalid;

        RejectionReason = reason;
        return Finish(RedemptionStatus.Rejected, $"Rejected: {reason}", at);
    }

    public void ClearDomainEvents() => _events.Clear();

    private TransitionResult ReservePoints(DateTimeOffset at)
    {
        Raise(new PointsReserved(Id, CustomerId, TotalPoints, at));
        return Move(RedemptionStatus.PointsReserved, "Delivery pending: points reserved locally", at);
    }

    private TransitionResult RequestCommit(DateTimeOffset at)
    {
        Raise(new PointsCommitRequested(Id, CustomerId, TotalPoints, at));
        return Move(RedemptionStatus.CommitPending, "Delivery held: deduction requested", at);
    }

    private TransitionResult ReleaseReservation(DateTimeOffset at)
    {
        // The points provider was never touched: releasing the local hold is enough.
        Raise(new ReservationReleased(Id, CustomerId, TotalPoints, at));
        return Finish(RedemptionStatus.Cancelled, "Delivery not completed: local reservation released", at);
    }

    private TransitionResult RequestFullReversal(DateTimeOffset at)
    {
        PointsToReverse = TotalPoints;
        StatusAfterReversal = RedemptionStatus.Cancelled;
        Raise(new PointsReversalRequested(Id, CustomerId, TotalPoints, IsPartial: false, at));
        return Move(RedemptionStatus.ReversalPending, "Delivery not completed: full reversal requested", at);
    }

    private TransitionResult RequestPartialReversal(IReadOnlyCollection<DeliveredLine>? delivered, DateTimeOffset at)
    {
        if (delivered is null || delivered.Count == 0)
            return TransitionResult.RejectedInvalid;

        // Reverse only the points of the items that were not delivered.
        var undeliveredPoints = _lines.Sum(line =>
        {
            var deliveredQty = delivered.Where(d => d.RewardSku == line.RewardSku).Sum(d => d.DeliveredQuantity);
            var missing = Math.Clamp(line.Quantity - deliveredQty, 0, line.Quantity);
            return missing * line.PointsPerUnit;
        });

        if (undeliveredPoints == 0)
            return Finish(RedemptionStatus.Completed, "Partial status but all items delivered", at);
        if (undeliveredPoints == TotalPoints)
            return RequestFullReversal(at);

        PointsToReverse = undeliveredPoints;
        StatusAfterReversal = RedemptionStatus.PartiallyCompleted;
        Raise(new PointsReversalRequested(Id, CustomerId, undeliveredPoints, IsPartial: true, at));
        return Move(RedemptionStatus.ReversalPending, $"Partial delivery: reversal of {undeliveredPoints} points requested", at);
    }

    private TransitionResult Finish(RedemptionStatus final, string trigger, DateTimeOffset at)
    {
        Raise(new RedemptionFinished(Id, final, at));
        return Move(final, trigger, at);
    }

    private TransitionResult Move(RedemptionStatus to, string trigger, DateTimeOffset at)
    {
        _history.Add(new StatusChange(Status, to, trigger, at));
        Status = to;
        UpdatedAt = at;
        return TransitionResult.Applied;
    }

    private void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);
}