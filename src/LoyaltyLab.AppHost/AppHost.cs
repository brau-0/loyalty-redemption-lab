var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql").WithDataVolume();
var redemptionDb = sql.AddDatabase("redemptiondb");

var cache = builder.AddRedis("cache");

var messaging = builder.AddAzureServiceBus("messaging").RunAsEmulator();
var redemptionEvents = messaging.AddServiceBusTopic("redemption-events");
redemptionEvents.AddServiceBusSubscription("orchestrator")
    .WithProperties(s =>
    {
        s.RequiresSession = true;   
        s.MaxDeliveryCount = 5;    
    });
redemptionEvents.AddServiceBusSubscription("notifications");


var customers = builder.AddProject<Projects.Mocks_Customers>("mock-customers");
var points = builder.AddProject<Projects.Mocks_Points>("mock-points");
var delivery = builder.AddProject<Projects.Mocks_Delivery>("mock-delivery");


builder.AddProject<Projects.Redemption_Api>("redemption-api")
    .WithReference(redemptionDb).WaitFor(redemptionDb)
    .WithReference(cache)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(customers);

builder.AddProject<Projects.Redemption_Worker>("redemption-worker")
    .WithReference(redemptionDb).WaitFor(redemptionDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(points)
    .WithReference(delivery);

builder.Build().Run();