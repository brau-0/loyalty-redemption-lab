using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Redemption.Infrastructure;

namespace Redemption.Worker;

/// <summary>
/// Publishes pending outbox messages to Service Bus. Delivery is at-least-once: a crash between
/// sending and marking the row as processed resends the message, so consumers must be idempotent.
/// </summary>
public sealed class OutboxRelay(
    IServiceScopeFactory scopeFactory, ServiceBusClient client, ILogger<OutboxRelay> logger) : BackgroundService
{
    private const string Topic = "redemption-events";
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sender = client.CreateSender(Topic);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishPendingAsync(sender, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Outbox relay cycle failed; retrying on next tick");
            }
        }
    }

    private async Task PublishPendingAsync(ServiceBusSender sender, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RedemptionDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.Sequence)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                await sender.SendMessageAsync(new ServiceBusMessage(message.Payload)
                {
                    MessageId = message.Id.ToString(),
                    SessionId = message.AggregateId.ToString(),
                    Subject = message.Type,
                    ContentType = "application/json"
                }, ct);

                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message[..Math.Min(ex.Message.Length, 2048)];
                logger.LogWarning(ex, "Failed to publish outbox message {MessageId}", message.Id);
                break; // Stop the batch to preserve ordering; retry on the next tick.
            }
        }

        await db.SaveChangesAsync(ct);
    }
}