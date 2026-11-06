using Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

// Local infrastructure (containers)
var sql = builder.AddSqlServer("sql").WithDataVolume();
var redemptionDb = sql.AddDatabase("redemptiondb");

var cache = builder.AddRedis("cache");

var messaging = builder.AddAzureServiceBus("messaging").RunAsEmulator();
var redemptionEvents = messaging.AddServiceBusTopic("redemption-events");

redemptionEvents.AddServiceBusSubscription("orchestrator")
    .WithProperties(s =>
    {
        s.RequiresSession = true;   // Ordering per redemption.
        s.MaxDeliveryCount = 5;     // Then dead-letter.
        AddSubjects(s, "DeliveryStatusChanged", "PointsCommitConfirmed", "PointsReversalConfirmed");
    });

redemptionEvents.AddServiceBusSubscription("points-adapter")
    .WithProperties(s =>
    {
        s.MaxDeliveryCount = 5;
        AddSubjects(s, "PointsCommitRequested", "PointsReversalRequested");
    });

redemptionEvents.AddServiceBusSubscription("notifications")
    .WithProperties(s => AddSubjects(s, "RedemptionFinished"));

// No filter: receives every event, used as an audit trail.
redemptionEvents.AddServiceBusSubscription("audit");

// Simulated external systems
var customers = builder.AddProject<Projects.Mocks_Customers>("mock-customers");
var points = builder.AddProject<Projects.Mocks_Points>("mock-points");
var delivery = builder.AddProject<Projects.Mocks_Delivery>("mock-delivery");

// Redemption platform
var api = builder.AddProject<Projects.Redemption_Api>("redemption-api")
    .WithReference(redemptionDb).WaitFor(redemptionDb)
    .WithReference(cache)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(customers);

builder.AddProject<Projects.Redemption_Worker>("redemption-worker")
    .WithReference(redemptionDb).WaitFor(redemptionDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(points)
    .WithReference(delivery)
    .WaitFor(api); // The API applies migrations in development before the relay starts.

builder.Build().Run();

// One correlation rule per subject: rules are OR-ed together.
static void AddSubjects(AzureServiceBusSubscriptionResource subscription, params string[] subjects)
{
    foreach (var subject in subjects)
    {
        subscription.Rules.Add(new AzureServiceBusRule($"subject-{subject}")
        {
            CorrelationFilter = new() { Subject = subject }
        });
    }
}