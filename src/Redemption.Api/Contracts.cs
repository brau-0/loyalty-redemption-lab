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
public sealed record RewardDto(string Sku, string Name, string Category, int Points);
public sealed record RedemptionSummary(
    Guid Id, RedemptionStatus Status, int TotalPoints, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);