using Redemption.Domain;

namespace Redemption.Functions;

/// <summary>Anti-corruption layer: the provider's vocabulary never goes past this class.</summary>
public static class DeliveryStatusTranslator
{
    public static DeliveryStatus? Translate(string providerStatus) => providerStatus.ToLowerInvariant() switch
    {
        "pending" => DeliveryStatus.Pending,
        "holded" => DeliveryStatus.Held,
        "ondelivery" => DeliveryStatus.OnDelivery,
        "completed" => DeliveryStatus.Completed,
        "completed_partially" => DeliveryStatus.CompletedPartially,
        "not_completed" => DeliveryStatus.NotCompleted,
        _ => null
    };
}