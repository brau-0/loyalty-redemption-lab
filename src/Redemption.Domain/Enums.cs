namespace Redemption.Domain;

/// <summary>Internal lifecycle of a redemption. This is our model, not the provider's.</summary>
public enum RedemptionStatus
{
    Created,
    PointsReserved,     // Points held in our local ledger.
    CommitPending,      // Waiting for the points provider to confirm the deduction.
    PointsCommitted,    // Points deducted in the points provider.
    InDelivery,
    ReversalPending,    // Waiting for the points provider to confirm a reversal.
    Completed,
    PartiallyCompleted,
    Cancelled,
    Rejected
}

/// <summary>
/// Delivery states as understood by our domain. The delivery adapter maps
/// provider-specific values to these (anti-corruption layer).
/// </summary>
public enum DeliveryStatus
{
    Pending,
    Held,
    OnDelivery,
    Completed,
    CompletedPartially,
    NotCompleted
}

/// <summary>Outcome of applying a transition to a redemption.</summary>
public enum TransitionResult
{
    Applied,
    IgnoredDuplicate,   // Same or older signal: safe no-op (idempotency, stale events).
    NotYetApplicable,   // Signal arrived ahead of its predecessor: retry later.
    RejectedInvalid     // Conflicting or malformed signal: must be investigated.
}
