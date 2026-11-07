using Redemption.Domain;

namespace Redemption.Domain.Tests;

public class RedemptionOrderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    // Total = (2 x 500) + (1 x 1000) = 2000 points.
    private static RedemptionOrder NewOrder() => RedemptionOrder.Create(
        Guid.NewGuid(), "C-100",
        [new RedemptionLine("CAP-01", 2, 500), new RedemptionLine("BOTTLE-01", 1, 1000)],
        T0);

    private static RedemptionOrder OrderInDelivery()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);
        order.ConfirmPointsCommitted(T0);
        order.ApplyDeliveryStatus(DeliveryStatus.OnDelivery, T0);
        return order;
    }

    [Fact]
    public void Create_computes_total_points()
    {
        var order = NewOrder();

        Assert.Equal(2000, order.TotalPoints);
        Assert.Equal(RedemptionStatus.Created, order.Status);
    }

    [Fact]
    public void Create_rejects_empty_lines() =>
        Assert.Throws<ArgumentException>(() => RedemptionOrder.Create(Guid.NewGuid(), "C-100", [], T0));

    [Fact]
    public void Happy_path_reaches_completed_and_raises_expected_events()
    {
        var order = OrderInDelivery();

        var result = order.ApplyDeliveryStatus(DeliveryStatus.Completed, T0);

        Assert.Equal(TransitionResult.Applied, result);
        Assert.Equal(RedemptionStatus.Completed, order.Status);
        Assert.Collection(order.DomainEvents,
      e => Assert.IsType<RedemptionRequested>(e),
      e => Assert.IsType<PointsReserved>(e),
      e => Assert.IsType<PointsCommitRequested>(e),
      e => Assert.IsType<RedemptionFinished>(e));



    }

    [Fact]
    public void Duplicate_delivery_status_is_ignored()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);

        var result = order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);

        Assert.Equal(TransitionResult.IgnoredDuplicate, result);
        Assert.Single(order.DomainEvents.OfType<PointsReserved>());
    }

    [Fact]
    public void Status_arriving_early_is_not_yet_applicable()
    {
        var order = NewOrder();

        var result = order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);

        Assert.Equal(TransitionResult.NotYetApplicable, result);
        Assert.Equal(RedemptionStatus.Created, order.Status);
    }

    [Fact]
    public void Out_of_order_status_applies_once_its_predecessor_arrives()
    {
        var order = NewOrder();

        var early = order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);
        var retried = order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);

        Assert.Equal(TransitionResult.NotYetApplicable, early);
        Assert.Equal(TransitionResult.Applied, retried);
        Assert.Equal(RedemptionStatus.CommitPending, order.Status);
    }

    [Fact]
    public void Stale_status_is_ignored()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);

        var result = order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);

        Assert.Equal(TransitionResult.IgnoredDuplicate, result);
        Assert.Equal(RedemptionStatus.CommitPending, order.Status);
    }

    [Fact]
    public void On_delivery_before_points_confirmation_is_not_yet_applicable()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);

        var result = order.ApplyDeliveryStatus(DeliveryStatus.OnDelivery, T0);

        Assert.Equal(TransitionResult.NotYetApplicable, result);
    }

    [Fact]
    public void Conflicting_final_status_is_rejected()
    {
        var order = OrderInDelivery();
        order.ApplyDeliveryStatus(DeliveryStatus.Completed, T0);

        var result = order.ApplyDeliveryStatus(DeliveryStatus.NotCompleted, T0);

        Assert.Equal(TransitionResult.RejectedInvalid, result);
        Assert.Equal(RedemptionStatus.Completed, order.Status);
    }

    [Fact]
    public void Not_completed_before_hold_only_releases_local_reservation()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);

        order.ApplyDeliveryStatus(DeliveryStatus.NotCompleted, T0);

        Assert.Equal(RedemptionStatus.Cancelled, order.Status);
        Assert.Single(order.DomainEvents.OfType<ReservationReleased>());
        Assert.Empty(order.DomainEvents.OfType<PointsReversalRequested>());
    }

    [Fact]
    public void Not_completed_after_commit_requests_full_reversal()
    {
        var order = OrderInDelivery();

        order.ApplyDeliveryStatus(DeliveryStatus.NotCompleted, T0);
        var reversal = Assert.IsType<PointsReversalRequested>(order.DomainEvents.Last());
        order.ConfirmReversal(T0);

        Assert.Equal(2000, reversal.Points);
        Assert.False(reversal.IsPartial);
        Assert.Equal(RedemptionStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Partial_delivery_reverses_only_undelivered_points()
    {
        var order = OrderInDelivery();

        order.ApplyDeliveryStatus(DeliveryStatus.CompletedPartially, T0,
            [new DeliveredLine("CAP-01", 2), new DeliveredLine("BOTTLE-01", 0)]);
        var reversal = Assert.IsType<PointsReversalRequested>(order.DomainEvents.Last());
        order.ConfirmReversal(T0);

        Assert.Equal(1000, reversal.Points);
        Assert.True(reversal.IsPartial);
        Assert.Equal(RedemptionStatus.PartiallyCompleted, order.Status);
    }

    [Fact]
    public void Partial_status_with_nothing_delivered_becomes_full_reversal()
    {
        var order = OrderInDelivery();

        order.ApplyDeliveryStatus(DeliveryStatus.CompletedPartially, T0, [new DeliveredLine("CAP-01", 0)]);
        var reversal = Assert.IsType<PointsReversalRequested>(order.DomainEvents.Last());

        Assert.False(reversal.IsPartial);
        Assert.Equal(2000, reversal.Points);
    }

    [Fact]
    public void Late_commit_confirmation_after_reversal_is_ignored()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.NotCompleted, T0); // Before the provider confirmed.

        var result = order.ConfirmPointsCommitted(T0);

        Assert.Equal(TransitionResult.IgnoredDuplicate, result);
        Assert.Equal(RedemptionStatus.ReversalPending, order.Status);
    }

    [Fact]
    public void Reject_is_only_allowed_before_points_are_touched()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);

        var result = order.Reject("Insufficient balance", T0);

        Assert.Equal(TransitionResult.RejectedInvalid, result);
        Assert.Equal(RedemptionStatus.PointsReserved, order.Status);
    }

    [Fact]
    public void Every_transition_is_recorded_in_history()
    {
        var order = OrderInDelivery();
        order.ApplyDeliveryStatus(DeliveryStatus.Completed, T0);

        // Created, PointsReserved, CommitPending, PointsCommitted, InDelivery, Completed.
        Assert.Equal(6, order.History.Count);
        Assert.Equal(RedemptionStatus.Completed, order.History[^1].To);
    }
    [Fact]
    public void Rejected_commit_cancels_and_requests_delivery_cancellation()
    {
        var order = NewOrder();
        order.ApplyDeliveryStatus(DeliveryStatus.Pending, T0);
        order.ApplyDeliveryStatus(DeliveryStatus.Held, T0);

        var result = order.RejectPointsCommit("INSUFFICIENT_BALANCE", T0);

        Assert.Equal(TransitionResult.Applied, result);
        Assert.Equal(RedemptionStatus.Cancelled, order.Status);
        Assert.Single(order.DomainEvents.OfType<ReservationReleased>());
        Assert.Single(order.DomainEvents.OfType<DeliveryCancellationRequested>());
    }

    [Fact]
    public void Rejection_after_commit_was_confirmed_is_invalid()
    {
        var order = OrderInDelivery();

        var result = order.RejectPointsCommit("INSUFFICIENT_BALANCE", T0);

        Assert.Equal(TransitionResult.RejectedInvalid, result);
        Assert.Equal(RedemptionStatus.InDelivery, order.Status);
    }
}