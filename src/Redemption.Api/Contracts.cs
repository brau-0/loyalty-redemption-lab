using Redemption.Domain;

namespace Redemption.Api;

public sealed record CreateRedemptionRequest(string CustomerId, List<CreateRedemptionLine>? Lines);
public sealed record CreateRedemptionLine(string RewardSku, int Quantity);
public sealed record DeliveryStatusRequest(DeliveryStatus Status, List<DeliveredLine>? Delivered);

public sealed record RedemptionResponse(
    Guid Id, string CustomerId, RedemptionStatus Status, int TotalPoints, int PointsToReverse,
    IReadOnlyList<RedemptionLine> Lines, IReadOnlyList<StatusChange> History)
{
    public static RedemptionResponse From(RedemptionOrder o) =>
        new(o.Id, o.CustomerId, o.Status, o.TotalPoints, o.PointsToReverse, o.Lines, o.History);
}

// Temporary in-memory catalog. Lab 5 replaces it with a cached catalog service.
public static class RewardCatalog
{
    private static readonly Dictionary<string, int> PointsBySku = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CAP-01"] = 500,
        ["BOTTLE-01"] = 1000,
        ["COOLER-01"] = 5000
    };

    public static bool Exists(string sku) => PointsBySku.ContainsKey(sku);
    public static int PointsFor(string sku) => PointsBySku[sku];
}