using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Redemption.Domain;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Functions;

public sealed record DeliveryWebhook(
    string EventId, string Reference, string Status, List<DeliveryWebhookItem>? DeliveredItems, DateTimeOffset OccurredAt);
public sealed record DeliveryWebhookItem(string Sku, int Quantity);

/// <summary>
/// Receives the delivery provider's webhooks. In Azure the provider authenticates with a
/// function key (locally, keys are not enforced).
/// </summary>
public sealed class DeliveryWebhookFunction(ServiceBusSender sender, ILogger<DeliveryWebhookFunction> logger)
{
    [Function("DeliveryWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhooks/delivery")] HttpRequest request,
        CancellationToken ct)
    {
        var webhook = await request.ReadFromJsonAsync<DeliveryWebhook>(ct);
        if (webhook is null || !Guid.TryParse(webhook.Reference, out var redemptionId))
            return new BadRequestObjectResult(new { error = "Invalid payload or reference." });

        if (DeliveryStatusTranslator.Translate(webhook.Status) is not { } status)
            return new BadRequestObjectResult(new { error = $"Unknown status '{webhook.Status}'." });

        var delivered = webhook.DeliveredItems?.Select(i => new DeliveredLine(i.Sku, i.Quantity)).ToList();
        var change = new DeliveryStatusChanged(redemptionId, status, delivered, webhook.OccurredAt);

        // The provider's event id becomes the MessageId, so provider retries are deduplicated by the inbox.
        await sender.SendMessageAsync(MessageFactory.Create(change, redemptionId, $"delivery-{webhook.EventId}"), ct);

        logger.LogInformation("Webhook {Status} ({EventId}) for {RedemptionId} accepted",
            webhook.Status, webhook.EventId, redemptionId);
        return new AcceptedResult();
    }
}