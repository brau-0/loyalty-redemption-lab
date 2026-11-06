using Azure.Messaging.ServiceBus;

namespace Redemption.Worker;

/// <summary>Logs every published event. Its subscription has no filter.</summary>
public sealed class AuditConsumer(ServiceBusClient client, ILogger<AuditConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor("redemption-events", "audit",
            new ServiceBusProcessorOptions { AutoCompleteMessages = false });

        processor.ProcessMessageAsync += async args =>
        {
            var message = args.Message;
            logger.LogInformation("Audit {Subject} for redemption {SessionId} (message {MessageId}): {Body}",
                message.Subject, message.SessionId, message.MessageId, message.Body.ToString());
            await args.CompleteMessageAsync(message);
        };
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Audit consumer error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }
}