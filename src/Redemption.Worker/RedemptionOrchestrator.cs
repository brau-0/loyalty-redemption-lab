using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Redemption.Domain;
using Redemption.Infrastructure;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Worker;

/// <summary>
/// Saga orchestrator. Sessions guarantee that messages for the same redemption are handled
/// one at a time and in order; the inbox makes handling idempotent.
/// </summary>
public sealed class RedemptionOrchestrator(
    IServiceScopeFactory scopeFactory, ServiceBusClient client, TimeProvider clock,
    ILogger<RedemptionOrchestrator> logger) : BackgroundService
{
    private const string ConsumerName = "orchestrator";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateSessionProcessor(Topics.RedemptionEvents, "orchestrator",
            new ServiceBusSessionProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentSessions = 16,                    // Different redemptions run in parallel.
                SessionIdleTimeout = TimeSpan.FromSeconds(5)   // Release idle sessions quickly.
            });

        processor.ProcessMessageAsync += HandleAsync;
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Orchestrator error ({Source})", args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }

    private async Task HandleAsync(ProcessSessionMessageEventArgs args)
    {
        var message = args.Message;
        var ct = args.CancellationToken;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RedemptionDbContext>();

        // Idempotency: an already handled message is completed without side effects.
        if (await db.InboxMessages.AnyAsync(i => i.MessageId == message.MessageId && i.Consumer == ConsumerName, ct))
        {
            logger.LogInformation("Duplicate message {MessageId} ignored", message.MessageId);
            await args.CompleteMessageAsync(message, ct);
            return;
        }

        if (!Guid.TryParse(message.SessionId, out var redemptionId))
        {
            await args.DeadLetterMessageAsync(message, "InvalidSessionId", "SessionId is not a redemption id.", ct);
            return;
        }

        var order = await db.Redemptions.FirstOrDefaultAsync(o => o.Id == redemptionId, ct);
        if (order is null)
        {
            await args.DeadLetterMessageAsync(message, "RedemptionNotFound", $"Redemption {redemptionId} does not exist.", ct);
            return;
        }

        TransitionResult result;
        try
        {
            result = Apply(order, message);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Permanent failure: retrying will never fix a malformed or unknown message.
            await args.DeadLetterMessageAsync(message, "MalformedMessage", ex.Message, ct);
            return;
        }

        if (result == TransitionResult.RejectedInvalid)
        {
            // Business-invalid signal for the current state: park it for investigation.
            await args.DeadLetterMessageAsync(message, "InvalidTransition",
                $"{message.Subject} is not valid while the redemption is {order.Status}.", ct);
            return;
        }

        // State change, ledger, outbox events and inbox entry commit in a single transaction.
        // Transient failures throw here: the message is abandoned and redelivered.
        db.InboxMessages.Add(new InboxMessage
        {
            MessageId = message.MessageId,
            Consumer = ConsumerName,
            ProcessedAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
        await args.CompleteMessageAsync(message, ct);

        logger.LogInformation("{Subject} for {RedemptionId}: {Result}, now {Status}",
            message.Subject, redemptionId, result, order.Status);
    }

    private static TransitionResult Apply(RedemptionOrder order, ServiceBusReceivedMessage message)
    {
        switch (message.Subject)
        {
            case nameof(DeliveryStatusChanged):
                var change = Read<DeliveryStatusChanged>(message);
                return order.ApplyDeliveryStatus(change.Status, change.OccurredAt, change.Delivered);
            case nameof(PointsCommitConfirmed):
                return order.ConfirmPointsCommitted(Read<PointsCommitConfirmed>(message).OccurredAt);
            case nameof(PointsReversalConfirmed):
                return order.ConfirmReversal(Read<PointsReversalConfirmed>(message).OccurredAt);
            case nameof(PointsCommitRejected):
                var rejection = Read<PointsCommitRejected>(message);
                return order.RejectPointsCommit(rejection.Reason, rejection.OccurredAt);
            default:
                throw new NotSupportedException($"Unsupported subject '{message.Subject}'.");
        }
    }

    private static T Read<T>(ServiceBusReceivedMessage message) =>
        message.Body.ToObjectFromJson<T>(OutboxJson.Options)
        ?? throw new JsonException($"Empty body for {typeof(T).Name}.");
}