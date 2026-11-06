using Microsoft.EntityFrameworkCore;
using Redemption.Domain;
using Redemption.Infrastructure;

namespace Redemption.Api;

// Development-only endpoints to drive the saga by hand until the provider mocks exist (Lab 3).
public static class DeliverySimulatorEndpoints
{
    public static void MapDeliverySimulatorEndpoints(this IEndpointRouteBuilder app)
    {
        var dev = app.MapGroup("/dev/redemptions/{id:guid}");

        dev.MapPost("/delivery-status", (Guid id, DeliveryStatusRequest request,
            RedemptionDbContext db, TimeProvider clock, CancellationToken ct) =>
            ApplyAsync(id, db, ct, order => order.ApplyDeliveryStatus(request.Status, clock.GetUtcNow(), request.Delivered)));

        dev.MapPost("/points-committed", (Guid id, RedemptionDbContext db, TimeProvider clock, CancellationToken ct) =>
            ApplyAsync(id, db, ct, order => order.ConfirmPointsCommitted(clock.GetUtcNow())));

        dev.MapPost("/reversal-confirmed", (Guid id, RedemptionDbContext db, TimeProvider clock, CancellationToken ct) =>
            ApplyAsync(id, db, ct, order => order.ConfirmReversal(clock.GetUtcNow())));
    }

    private static async Task<IResult> ApplyAsync(
        Guid id, RedemptionDbContext db, CancellationToken ct, Func<RedemptionOrder, TransitionResult> transition)
    {
        var order = await db.Redemptions.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return Results.NotFound();

        var result = transition(order);
        if (result == TransitionResult.RejectedInvalid)
            return Results.Conflict(new { result, order.Status });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another process changed the order: the caller must retry with fresh state.
            return Results.Conflict(new { error = "Concurrent update, retry." });
        }

        return Results.Ok(new { result, status = order.Status, order.PointsToReverse });
    }
}