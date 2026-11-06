using Azure.Messaging.ServiceBus;
using Redemption.Domain;
using Redemption.Infrastructure.Messaging;

namespace Redemption.Worker;

/// <summary>
/// Adapter to the points provider. It currently simulates a successful call; Lab 3 replaces
/// the simulation with a real HTTP call to the points mock. Replies use deterministic
/// MessageIds, so a retried request never produces two distinct replies.
/// </summary>
public sealed class PointsAdapter(ServiceBusClient client, TimeProvider clock, ILogger<PointsAdapter> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sender = client.CreateSender(Topics.RedemptionEvents);
        await using var processor = client.CreateProcessor(Topics.RedemptionEvents, "points-adapter",
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 8 });

        processor.ProcessMessageAsync += async args =>
        {
            var request = args.Message;
            var redemptionId = Guid.Parse(request.SessionId);

            object reply = request.Subject switch
            {
                nameof(PointsCommitRequested) => new PointsCommitConfirmed(redemptionId, clock.GetUtcNow()),
                nameof(PointsReversalRequested) => new PointsReversalConfirmed(redemptionId, clock.GetUtcNow()),
                _ => throw new NotSupportedException($"Unsupported subject '{request.Subject}'.")
            };

            // Simulated provider latency. Lab 3: real HTTP call to the points mock.
            await Task.Delay(TimeSpan.FromMilliseconds(300), args.CancellationToken);

            await sender.SendMessageAsync(
                MessageFactory.Create(reply, redemptionId, messageId: $"{request.MessageId}-reply"),
                args.CancellationToken);
            await args.CompleteMessageAsync(request, args.CancellationToken);

            logger.LogInformation("{Request} for {RedemptionId} confirmed by the points provider",
                request.Subject, redemptionId);
        };
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
}