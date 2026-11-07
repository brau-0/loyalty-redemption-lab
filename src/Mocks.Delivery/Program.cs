using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("webhooks");
builder.Services.AddSingleton<ShipmentStore>();
builder.Services.AddSingleton<ScenarioSettingsHolder>();
builder.Services.AddSingleton<ShipmentLifecycle>();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapPost("/shipments", (ShipmentRequest request, ShipmentStore store, ShipmentLifecycle lifecycle) =>
{
    // Idempotent by reference: a retried creation returns the existing shipment.
    var shipment = store.GetOrAdd(request, out var created);
    if (!created)
        return Results.Ok(new { request.Reference, status = "existing" });

    lifecycle.Start(shipment);
    return Results.Created($"/shipments/{request.Reference}", new { request.Reference, status = "created" });
});

app.MapPost("/shipments/{reference}/cancel", (string reference, ShipmentStore store) => store.Cancel(reference));

app.MapGet("/shipments/{reference}", (string reference, ShipmentStore store) =>
    store.Get(reference) is { } s ? Results.Ok(new { reference, s.Cancelled, sent = s.Sent }) : Results.NotFound());

// Scenario and chaos for shipments created from now on.
app.MapGet("/admin/settings", (ScenarioSettingsHolder holder) => holder.Current);
app.MapPost("/admin/settings", (ScenarioSettings settings, ScenarioSettingsHolder holder) =>
{
    holder.Current = settings;
    return Results.Ok(settings);
});

app.Run();

sealed record ShipmentRequest(string Reference, string CustomerId, List<ShipmentItem> Items);
sealed record ShipmentItem(string Sku, int Quantity);
sealed record DeliveredItem(string Sku, int Quantity);
sealed record WebhookEvent(string EventId, string Reference, string Status, List<DeliveredItem>? DeliveredItems, DateTimeOffset OccurredAt);

/// <summary>Scenario: happy, partial, notCompletedBeforeHold, notCompletedAfterHold.</summary>
sealed record ScenarioSettings(string Scenario = "happy", double DuplicateRate = 0, double SwapRate = 0, int StepDelayMs = 1500);

sealed class ScenarioSettingsHolder
{
    public ScenarioSettings Current { get; set; } = new();
}

sealed class Shipment(ShipmentRequest request)
{
    public ShipmentRequest Request { get; } = request;
    public HashSet<string> Sent { get; } = new();
    public bool Cancelled { get; set; }
}

sealed class ShipmentStore
{
    private static readonly string[] PastWarehouse = ["ondelivery", "completed", "completed_partially", "not_completed"];
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Shipment> _shipments = new();

    public Shipment GetOrAdd(ShipmentRequest request, out bool created)
    {
        lock (_gate)
        {
            created = !_shipments.TryGetValue(request.Reference, out var shipment);
            if (created)
                _shipments[request.Reference] = shipment = new Shipment(request);
            return shipment!;
        }
    }

    public Shipment? Get(string reference)
    {
        lock (_gate)
            return _shipments.GetValueOrDefault(reference);
    }

    public IResult Cancel(string reference)
    {
        lock (_gate)
        {
            if (!_shipments.TryGetValue(reference, out var shipment))
                return Results.NotFound();
            // Once the goods left the warehouse, the shipment can no longer be cancelled.
            if (shipment.Sent.Overlaps(PastWarehouse))
                return Results.Conflict(new { code = "TOO_LATE", sent = shipment.Sent });

            shipment.Cancelled = true;
            return Results.Ok(new { reference, cancelled = true });
        }
    }

    /// <summary>Records a status about to be sent. Returns false if the shipment was cancelled.</summary>
    public bool TryAdvance(Shipment shipment, string status)
    {
        lock (_gate)
        {
            if (shipment.Cancelled)
                return false;
            shipment.Sent.Add(status);
            return true;
        }
    }
}

/// <summary>Drives each shipment through its scenario and posts webhooks, with optional chaos.</summary>
sealed class ShipmentLifecycle(
    IHttpClientFactory httpFactory, ScenarioSettingsHolder settings, ShipmentStore store,
    IConfiguration configuration, IHostApplicationLifetime lifetime, ILogger<ShipmentLifecycle> logger)
{
    // Real providers register one webhook URL per client. Lab 4 points it to an Azure Function.
    private readonly string _webhookUrl = configuration["WebhookUrl"] ?? "https+http://redemption-api/webhooks/delivery";

    public void Start(Shipment shipment)
    {
        var current = settings.Current;
        var plan = BuildPlan(shipment.Request, current);
        _ = Task.Run(() => RunAsync(shipment, plan, current.StepDelayMs, lifetime.ApplicationStopping));
    }

    private static List<WebhookEvent> BuildPlan(ShipmentRequest request, ScenarioSettings s)
    {
        string[] statuses = s.Scenario switch
        {
            "partial" => ["pending", "holded", "ondelivery", "completed_partially"],
            "notCompletedBeforeHold" => ["pending", "not_completed"],
            "notCompletedAfterHold" => ["pending", "holded", "ondelivery", "not_completed"],
            _ => ["pending", "holded", "ondelivery", "completed"]
        };

        var events = statuses.Select(status => new WebhookEvent(
            Guid.NewGuid().ToString(), request.Reference, status,
            status == "completed_partially"
                // Partial delivery: only the first item arrives.
                ? request.Items.Select((item, i) => new DeliveredItem(item.Sku, i == 0 ? item.Quantity : 0)).ToList()
                : null,
            DateTimeOffset.UtcNow)).ToList();

        // Out-of-order: a later status overtakes the previous one.
        if (events.Count > 1 && Random.Shared.NextDouble() < s.SwapRate)
        {
            var i = Random.Shared.Next(events.Count - 1);
            (events[i], events[i + 1]) = (events[i + 1], events[i]);
        }

        // Duplicates: some events are resent later with the same EventId.
        foreach (var e in events.ToList())
        {
            if (Random.Shared.NextDouble() < s.DuplicateRate)
                events.Insert(Random.Shared.Next(events.IndexOf(e) + 1, events.Count + 1), e);
        }

        return events;
    }

    private async Task RunAsync(Shipment shipment, List<WebhookEvent> plan, int stepDelayMs, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("webhooks");

        foreach (var webhook in plan)
        {
            await Task.Delay(stepDelayMs, ct);

            if (!store.TryAdvance(shipment, webhook.Status))
            {
                logger.LogInformation("Shipment {Reference} cancelled; lifecycle stopped", shipment.Request.Reference);
                return;
            }

            var payload = webhook with { OccurredAt = DateTimeOffset.UtcNow };
            try
            {
                using var response = await http.PostAsJsonAsync(_webhookUrl, payload, ct);
                logger.LogInformation("Webhook {Status} ({EventId}) for {Reference} -> {Code}",
                    payload.Status, payload.EventId, payload.Reference, (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Webhook {Status} for {Reference} failed", payload.Status, payload.Reference);
            }
        }
    }
}