using System.Net;
using System.Net.Http.Json;

namespace Redemption.Functions;

public sealed record ShipmentState(
    string Reference, bool Cancelled, List<string> Statuses, List<DeliveryWebhookItem>? DeliveredItems);

/// <summary>Queries the delivery provider for the real state of a shipment.</summary>
public sealed class DeliveryStatusClient(HttpClient http)
{
    public async Task<ShipmentState?> GetAsync(Guid reference, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/shipments/{reference}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShipmentState>(ct);
    }
}