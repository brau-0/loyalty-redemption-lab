using Microsoft.EntityFrameworkCore;
using Redemption.Infrastructure;

namespace Redemption.Api;

public sealed record BalanceView(string CustomerId, int ProviderBalance, int ReservedPoints, int AvailablePoints);

public sealed record Eligibility(bool IsEligible, string? Code, string? Message)
{
    public static readonly Eligibility Ok = new(true, null, null);
    public static Eligibility Denied(string code, string message) => new(false, code, message);
}

/// <summary>
/// Fast feedback before creating a redemption. It is an optimization, not the guarantee:
/// concurrent redemptions can both pass, and the points provider remains the source of truth.
/// </summary>
public sealed class RedemptionEligibility(CustomersClient customers, PointsBalanceClient points, RedemptionDbContext db)
{
    public async Task<Eligibility> CheckAsync(string customerId, int requiredPoints, CancellationToken ct)
    {
        // Independent lookups run in parallel to keep latency low.
        var customerTask = customers.GetAsync(customerId, ct);
        var balanceTask = GetBalanceAsync(customerId, ct);
        await Task.WhenAll(customerTask, balanceTask);

        var customer = customerTask.Result;
        if (customer is null)
            return Eligibility.Denied("CUSTOMER_NOT_FOUND", $"Customer {customerId} does not exist.");
        if (!string.Equals(customer.Status, "active", StringComparison.OrdinalIgnoreCase))
            return Eligibility.Denied("CUSTOMER_BLOCKED", $"Customer {customerId} is {customer.Status}.");

        var balance = balanceTask.Result;
        if (balance is null)
            return Eligibility.Denied("NO_POINTS_ACCOUNT", $"Customer {customerId} has no points account.");
        if (balance.AvailablePoints < requiredPoints)
            return Eligibility.Denied("INSUFFICIENT_BALANCE",
                $"Available {balance.AvailablePoints} points, required {requiredPoints}.");

        return Eligibility.Ok;
    }

    /// <summary>Available = provider balance minus local reservations not yet deducted by the provider.</summary>
    public async Task<BalanceView?> GetBalanceAsync(string customerId, CancellationToken ct)
    {
        var providerBalance = await points.GetBalanceAsync(customerId, ct);
        if (providerBalance is null)
            return null;

        var reserved = await db.PointsReservations
            .Where(r => r.CustomerId == customerId && r.State == ReservationState.Active)
            .SumAsync(r => r.Points, ct);

        return new BalanceView(customerId, providerBalance.Value, reserved, providerBalance.Value - reserved);
    }
}