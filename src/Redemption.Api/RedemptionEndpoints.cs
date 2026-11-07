using Microsoft.EntityFrameworkCore;
using Polly;
using Redemption.Domain;
using Redemption.Infrastructure;

namespace Redemption.Api;

public static class RedemptionEndpoints
{
    public static void MapRedemptionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/redemptions", async (
    CreateRedemptionRequest request, RedemptionEligibility eligibility,
    RedemptionDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var requested = request.Lines ?? [];

            // Points per unit always come from our catalog, never from the client.
            var unknown = requested.Where(l => !RewardCatalog.Exists(l.RewardSku)).Select(l => l.RewardSku).ToList();
            if (unknown.Count > 0)
                return Results.Problem($"Unknown rewards: {string.Join(", ", unknown)}", statusCode: StatusCodes.Status400BadRequest);

            var lines = requested
                .Select(l => new RedemptionLine(l.RewardSku, l.Quantity, RewardCatalog.PointsFor(l.RewardSku)))
                .ToList();

            try
            {
                var check = await eligibility.CheckAsync(request.CustomerId, lines.Sum(l => l.TotalPoints), ct);
                if (!check.IsEligible)
                {
                    return Results.Problem(check.Message, title: check.Code,
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        extensions: new Dictionary<string, object?> { ["code"] = check.Code });
                }

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
            catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException)
            {
                // Retries and the circuit breaker gave up: fail fast and let the client try again later.
                return Results.Problem("Customer or points service unavailable. Try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/redemptions/{id:guid}", async (Guid id, RedemptionDbContext db, CancellationToken ct) =>
            await db.Redemptions.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct) is { } order
                ? Results.Ok(RedemptionResponse.From(order))
                : Results.NotFound());

        app.MapGet("/customers/{customerId}/balance", async (
            string customerId, RedemptionEligibility eligibility, CancellationToken ct) =>
        await eligibility.GetBalanceAsync(customerId, ct) is { } balance
        ? Results.Ok(balance)
        : Results.NotFound());
    }
}