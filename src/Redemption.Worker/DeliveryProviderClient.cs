using System.Net;
using System.Net.Http.Json;
using Redemption.Domain;

namespace Redemption.Worker;

public enum CancellationOutcome { Cancelled, NotFound, TooLate }

public sealed class DeliveryProviderClient(HttpClient http)
{
    public async Task CreateShipmentAsync(Guid reference, string customerId, IEnumerable<RedemptionLine> lines, CancellationToken ct)
    {
        var body = new
        {
            reference = reference.ToString(),
            customerId,
            items = lines.Select(l => new { sku = l.RewardSku, quantity = l.Quantity })
        };
        using var response = await http.PostAsJsonAsync("/shipments", body, ct);
        response.EnsureSuccessStatusCode(); // Idempotent by reference: 201 created or 200 existing.
    }

    public async Task<CancellationOutcome> CancelShipmentAsync(Guid reference, CancellationToken ct)
    {
        using var response = await http.PostAsync($"/shipments/{reference}/cancel", content: null, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => CancellationOutcome.Cancelled,
            HttpStatusCode.NotFound => CancellationOutcome.NotFound,
            HttpStatusCode.Conflict => CancellationOutcome.TooLate,
            _ => throw new HttpRequestException($"Unexpected {(int)response.StatusCode} cancelling shipment {reference}.")
        };
    }
}