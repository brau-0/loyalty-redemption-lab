using Azure.Messaging.ServiceBus;

namespace Redemption.Worker;

sealed class SmokeConsumer(ServiceBusClient client, ILogger<SmokeConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateSessionProcessor(
            "redemption-events", "orchestrator",
            new ServiceBusSessionProcessorOptions { AutoCompleteMessages = false, MaxConcurrentSessions = 8 });

        processor.ProcessMessageAsync += async args =>
        {
            logger.LogInformation("Sesión {SessionId}: {Body}", args.SessionId, args.Message.Body.ToString());
            await args.CompleteMessageAsync(args.Message);
        };
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Error procesando mensajes");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }
}