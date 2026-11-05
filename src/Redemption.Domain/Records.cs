namespace Redemption.Domain;

public sealed record RedemptionLine(string RewardSku, int Quantity, int PointsPerUnit)
{
    public int TotalPoints => Quantity * PointsPerUnit;
}

public sealed record DeliveredLine(string RewardSku, int DeliveredQuantity);

/// <summary>Audit trail entry for every state change.</summary>
public sealed record StatusChange(RedemptionStatus? From, RedemptionStatus To, string Trigger, DateTimeOffset At);