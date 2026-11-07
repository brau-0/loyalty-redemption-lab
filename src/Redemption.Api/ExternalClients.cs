using System.Net;
using System.Net.Http.Json;

namespace Redemption.Api;

public sealed record CustomerInfo(string Id, string Name, string Status, string Country);

public sealed class CustomersClient(HttpClient http)
{
    public async Task<CustomerInfo?> GetAsync(string customerId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/customers/{Uri.EscapeDataString(customerId)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerInfo>(ct);
    }
}

public sealed class PointsBalanceClient(HttpClient http)
{
    public async Task<int?> GetBalanceAsync(string customerId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/accounts/{Uri.EscapeDataString(customerId)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountDto>(ct))?.Balance;
    }

    private sealed record AccountDto(string CustomerId, int Balance);
}