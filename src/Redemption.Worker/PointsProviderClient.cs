using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Redemption.Worker;

public sealed record ProviderOutcome(bool Succeeded, string? ErrorCode)
{
    public static readonly ProviderOutcome Success = new(true, null);
    public static ProviderOutcome Rejected(string code) => new(false, code);
}

/// <summary>
/// Typed client for the points provider. Transient failures (5xx, timeouts) are retried by the
/// standard resilience handler from ServiceDefaults; if they persist, this throws and the
/// message is redelivered by Service Bus. Business rejections (422) are returned, never retried.
/// </summary>
public sealed class PointsProviderClient(HttpClient http)
{
    public Task<ProviderOutcome> DeductAsync(string customerId, string reference, int points, CancellationToken ct) =>
        SendAsync($"/accounts/{customerId}/deductions", new { reference, points }, ct);

    public Task<ProviderOutcome> ReverseAsync(
        string customerId, string reference, string originalReference, int points, CancellationToken ct) =>
        SendAsync($"/accounts/{customerId}/reversals", new { reference, originalReference, points }, ct);

    private async Task<ProviderOutcome> SendAsync(string path, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, ct);

        if (response.IsSuccessStatusCode)
            return ProviderOutcome.Success;

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.Content.ReadFromJsonAsync<ProviderError>(ct);
            return ProviderOutcome.Rejected(error?.Code ?? "UNKNOWN_REJECTION");
        }

        response.EnsureSuccessStatusCode();
        throw new UnreachableException();
    }

    private sealed record ProviderError(string Code, string Message);
}