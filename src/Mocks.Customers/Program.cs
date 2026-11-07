var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

// Fictional customer master data (stands in for the corporate customer system).
var customers = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase)
{
    ["C-100"] = new("C-100", "Abarrotes La Esperanza", "active", "MX"),
    ["C-200"] = new("C-200", "Tienda Don Pepe", "active", "MX"),
    ["C-300"] = new("C-300", "Miscelanea El Sol", "blocked", "MX")
};
var chaos = new ChaosSettings();

app.MapGet("/customers/{id}", async (string id) =>
{
    await Task.Delay(chaos.LatencyMs);
    if (Random.Shared.NextDouble() < chaos.FailureRate)
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    return customers.TryGetValue(id, out var customer) ? Results.Ok(customer) : Results.NotFound();
});

app.MapGet("/admin/chaos", () => chaos);
app.MapPost("/admin/chaos", (ChaosSettings settings) =>
{
    chaos = settings;
    return Results.Ok(settings);
});

app.Run();

sealed record Customer(string Id, string Name, string Status, string Country);
sealed record ChaosSettings(double FailureRate = 0, int LatencyMs = 50);