using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Redemption.Domain;
using Redemption.Infrastructure;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Worker;

/// <summary>
/// Adapter to the points provider. It uses deterministic business references, so retries are
/// idempotent on the provider side, and deterministic reply ids, so retries never produce
/// two distinct replies.
/// </summary>
public sealed class PointsAdapter(
    IServiceScopeFactory scopeFactory, ServiceBusClient client, TimeProvider clock, ILogger<PointsAdapter> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sender = client.CreateSender(Topics.RedemptionEvents);
        await using var processor = client.CreateProcessor(Topics.RedemptionEvents, "points-adapter",
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 8 });

        processor.ProcessMessageAsync += args => HandleAsync(args, sender);
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Points adapter error ({Source})", args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }

    private async Task HandleAsync(ProcessMessageEventArgs args, ServiceBusSender sender)
    {
        var request = args.Message;
        var ct = args.CancellationToken;
        await using var scope = scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<PointsProviderClient>();

        Guid redemptionId;
        object reply;

        switch (request.Subject)
        {
            case nameof(PointsCommitRequested):
                {
                    var command = Read<PointsCommitRequested>(request);
                    redemptionId = command.RedemptionId;
                    var outcome = await provider.DeductAsync(
                        command.CustomerId, CommitReference(redemptionId), command.Points, ct);

                    reply = outcome.Succeeded
                        ? new PointsCommitConfirmed(redemptionId, clock.GetUtcNow())
                        : new PointsCommitRejected(redemptionId, outcome.ErrorCode!, clock.GetUtcNow());
                    break;
                }
            case nameof(PointsReversalRequested):
                {
                    var command = Read<PointsReversalRequested>(request);
                    redemptionId = command.RedemptionId;
                    var outcome = await provider.ReverseAsync(
                        command.CustomerId, ReversalReference(redemptionId), CommitReference(redemptionId), command.Points, ct);

                    // If nothing was ever deducted, there is nothing to reverse: that counts as success.
                    if (!outcome.Succeeded && outcome.ErrorCode != "ORIGINAL_NOT_FOUND")
                    {
                        // A reversal must never be silently dropped: park it for manual follow-up.
                        await args.DeadLetterMessageAsync(request, "ReversalRejected", outcome.ErrorCode, ct);
                        return;
                    }

                    reply = new PointsReversalConfirmed(redemptionId, clock.GetUtcNow());
                    break;
                }
            default:
                await args.DeadLetterMessageAsync(request, "UnsupportedSubject", request.Subject, ct);
                return;
        }

        await sender.SendMessageAsync(MessageFactory.Create(reply, redemptionId, $"{request.MessageId}-reply"), ct);
        await args.CompleteMessageAsync(request, ct);

        logger.LogInformation("{Request} for {RedemptionId} -> {Reply}",
            request.Subject, redemptionId, reply.GetType().Name);
    }

    private static string CommitReference(Guid redemptionId) => $"{redemptionId}-commit";
    private static string ReversalReference(Guid redemptionId) => $"{redemptionId}-reversal";

    private static T Read<T>(ServiceBusReceivedMessage message) =>
        message.Body.ToObjectFromJson<T>(OutboxJson.Options)
        ?? throw new JsonException($"Empty body for {typeof(T).Name}.");
}