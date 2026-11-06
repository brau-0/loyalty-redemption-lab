var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<PointsStore>();
builder.Services.AddSingleton<Chaos>();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/accounts/{customerId}", (string customerId, PointsStore store) =>
    store.GetAccount(customerId) is { } account ? Results.Ok(account) : Results.NotFound());

app.MapPost("/accounts/{customerId}/deductions", (string customerId, DeductionRequest request, PointsStore store, Chaos chaos) =>
    chaos.RunAsync(() => store.Deduct(customerId, request.Reference, request.Points)));

app.MapPost("/accounts/{customerId}/reversals", (string customerId, ReversalRequest request, PointsStore store, Chaos chaos) =>
    chaos.RunAsync(() => store.Reverse(customerId, request.Reference, request.OriginalReference, request.Points)));

// Tune failure injection at runtime.
app.MapGet("/admin/chaos", (Chaos chaos) => chaos.Settings);
app.MapPost("/admin/chaos", (ChaosSettings settings, Chaos chaos) =>
{
    chaos.Settings = settings;
    return Results.Ok(settings);
});

app.Run();

sealed record DeductionRequest(string Reference, int Points);
sealed record ReversalRequest(string Reference, string OriginalReference, int Points);
sealed record ChaosSettings(double FailureRate = 0, double LostResponseRate = 0, int LatencyMs = 100);
sealed record Transaction(string Reference, string Kind, int Points, string? OriginalReference, int BalanceAfter, DateTimeOffset At);

/// <summary>Simulates latency, transient failures before processing, and lost responses after processing.</summary>
sealed class Chaos
{
    public ChaosSettings Settings { get; set; } = new();

    public async Task<IResult> RunAsync(Func<IResult> operation)
    {
        var settings = Settings;
        await Task.Delay(settings.LatencyMs);

        if (Random.Shared.NextDouble() < settings.FailureRate)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);   // Nothing was processed.

        var result = operation();

        if (Random.Shared.NextDouble() < settings.LostResponseRate)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);   // Processed, but the caller never learns it.

        return result;
    }
}

/// <summary>In-memory points provider. Every operation is idempotent by its business reference.</summary>
sealed class PointsStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _balances = new() { ["C-100"] = 10_000, ["C-200"] = 1_000 };
    private readonly Dictionary<string, Transaction> _byReference = new();
    private readonly Dictionary<string, List<Transaction>> _history = new();

    public object? GetAccount(string customerId)
    {
        lock (_gate)
        {
            if (!_balances.TryGetValue(customerId, out var balance))
                return null;
            var transactions = _history.TryGetValue(customerId, out var list) ? list.ToList() : new List<Transaction>();
            return new { customerId, balance, transactions };
        }
    }

    public IResult Deduct(string customerId, string reference, int points)
    {
        lock (_gate)
        {
            // A retried request returns the original outcome instead of deducting twice.
            if (_byReference.TryGetValue(reference, out var existing))
                return Results.Ok(existing);

            if (!_balances.TryGetValue(customerId, out var balance))
                return Error("UNKNOWN_CUSTOMER", $"Customer {customerId} does not exist.");
            if (points <= 0)
                return Error("INVALID_POINTS", "Points must be positive.");
            if (balance < points)
                return Error("INSUFFICIENT_BALANCE", $"Balance {balance} is lower than {points}.");

            _balances[customerId] = balance - points;
            return Record(customerId, new Transaction(reference, "Deduction", points, null, balance - points, DateTimeOffset.UtcNow));
        }
    }

    public IResult Reverse(string customerId, string reference, string originalReference, int points)
    {
        lock (_gate)
        {
            if (_byReference.TryGetValue(reference, out var existing))
                return Results.Ok(existing);

            if (!_byReference.TryGetValue(originalReference, out var original) || original.Kind != "Deduction")
                return Error("ORIGINAL_NOT_FOUND", $"No deduction with reference {originalReference}.");

            var alreadyReversed = _byReference.Values
                .Where(t => t.OriginalReference == originalReference)
                .Sum(t => t.Points);
            if (points <= 0 || points > original.Points - alreadyReversed)
                return Error("EXCEEDS_ORIGINAL", "Reversal exceeds the remaining deducted points.");

            var balance = _balances[customerId] + points;
            _balances[customerId] = balance;
            return Record(customerId, new Transaction(reference, "Reversal", points, originalReference, balance, DateTimeOffset.UtcNow));
        }
    }

    private IResult Record(string customerId, Transaction transaction)
    {
        _byReference[transaction.Reference] = transaction;
        if (!_history.TryGetValue(customerId, out var list))
            _history[customerId] = list = new List<Transaction>();
        list.Add(transaction);
        return Results.Created($"/accounts/{customerId}", transaction);
    }

    private static IResult Error(string code, string message) =>
        Results.UnprocessableEntity(new { code, message });
}