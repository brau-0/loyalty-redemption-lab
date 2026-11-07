using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Redemption.Domain;
using Redemption.Infrastructure;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Worker;

/// <summary>Creates and cancels shipments in the delivery provider. Status updates come back via webhooks.</summary>
public sealed class DeliveryAdapter(
    IServiceScopeFactory scopeFactory, ServiceBusClient client, ILogger<DeliveryAdapter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor(Topics.RedemptionEvents, "delivery-adapter",
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 8 });

        processor.ProcessMessageAsync += HandleAsync;
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Delivery adapter error ({Source})", args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }

    private async Task HandleAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var ct = args.CancellationToken;
        await using var scope = scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<DeliveryProviderClient>();

        switch (message.Subject)
        {
            case nameof(RedemptionRequested):
                {
                    var request = Read<RedemptionRequested>(message);
                    await provider.CreateShipmentAsync(request.RedemptionId, request.CustomerId, request.Lines, ct);
                    logger.LogInformation("Shipment created for redemption {RedemptionId}", request.RedemptionId);
                    break;
                }
            case nameof(DeliveryCancellationRequested):
                {
                    var request = Read<DeliveryCancellationRequested>(message);
                    var outcome = await provider.CancelShipmentAsync(request.RedemptionId, ct);
                    if (outcome == CancellationOutcome.TooLate)
                    {
                        // Goods are already on their way without points deducted: needs a human decision.
                        await args.DeadLetterMessageAsync(message, "CancellationTooLate",
                            $"Shipment {request.RedemptionId} already left the warehouse.", ct);
                        return;
                    }
                    logger.LogInformation("Shipment cancellation for {RedemptionId}: {Outcome}", request.RedemptionId, outcome);
                    break;
                }
            default:
                await args.DeadLetterMessageAsync(message, "UnsupportedSubject", message.Subject, ct);
                return;
        }

        await args.CompleteMessageAsync(message, ct);
    }

    private static T Read<T>(ServiceBusReceivedMessage message) =>
        message.Body.ToObjectFromJson<T>(OutboxJson.Options)
        ?? throw new JsonException($"Empty body for {typeof(T).Name}.");
}