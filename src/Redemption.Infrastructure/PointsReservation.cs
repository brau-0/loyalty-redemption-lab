using Redemption.Domain;

namespace Redemption.Infrastructure;

public enum ReservationState { Active, Settled, Released }

/// <summary>Local points hold for a redemption. One row per redemption (natural idempotency).</summary>
public sealed class PointsReservation
{
    public Guid RedemptionId { get; set; }
    public string CustomerId { get; set; } = default!;
    public int Points { get; set; }
    public ReservationState State { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class PointsLedger
{
    /// <summary>Derives the local reservation state from the redemption lifecycle.</summary>
    public static ReservationState? DesiredState(RedemptionOrder order) => order.Status switch
    {
        RedemptionStatus.Created or RedemptionStatus.Rejected => null,
        RedemptionStatus.PointsReserved or RedemptionStatus.CommitPending => ReservationState.Active,
        // Once the provider confirmed the deduction, its balance is the source of truth.
        _ when order.History.Any(h => h.To == RedemptionStatus.PointsCommitted) => ReservationState.Settled,
        _ => ReservationState.Released
    };
}