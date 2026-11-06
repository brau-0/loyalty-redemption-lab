using Microsoft.EntityFrameworkCore;
using Redemption.Domain;
using Redemption.Infrastructure;

namespace Redemption.Api;

public static class RedemptionEndpoints
{
    public static void MapRedemptionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/redemptions", async (
            CreateRedemptionRequest request, RedemptionDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var requested = request.Lines ?? [];

            // Points per unit always come from our catalog, never from the client.
            var unknown = requested.Where(l => !RewardCatalog.Exists(l.RewardSku)).Select(l => l.RewardSku).ToList();
            if (unknown.Count > 0)
                return Results.Problem($"Unknown rewards: {string.Join(", ", unknown)}", statusCode: StatusCodes.Status400BadRequest);

            try
            {
                var lines = requested.Select(l =>
                    new RedemptionLine(l.RewardSku, l.Quantity, RewardCatalog.PointsFor(l.RewardSku)));

                // Version 7 GUIDs are time-ordered, which keeps the clustered index efficient.
                var order = RedemptionOrder.Create(Guid.CreateVersion7(), request.CustomerId, lines, clock.GetUtcNow());

                db.Redemptions.Add(order);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/redemptions/{order.Id}", RedemptionResponse.From(order));
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapGet("/redemptions/{id:guid}", async (Guid id, RedemptionDbContext db, CancellationToken ct) =>
            await db.Redemptions.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct) is { } order
                ? Results.Ok(RedemptionResponse.From(order))
                : Results.NotFound());

        app.MapGet("/customers/{customerId}/reserved-points", async (
            string customerId, RedemptionDbContext db, CancellationToken ct) =>
        {
            var reserved = await db.PointsReservations
                .Where(r => r.CustomerId == customerId && r.State == ReservationState.Active)
                .SumAsync(r => r.Points, ct);
            return Results.Ok(new { customerId, reservedPoints = reserved });
        });
    }
}