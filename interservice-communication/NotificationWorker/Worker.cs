using Azure.Messaging.ServiceBus;
using Contracts;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;

    public Worker(IConfiguration configuration, ILogger<Worker> logger)
    {
        _logger = logger;

        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
        var topicName = configuration["ServiceBus:OrdersTopic"] ?? "orders-topic";
        var subscriptionName = configuration["ServiceBus:Subscription"] ?? "notification-subscription";

        _client = new ServiceBusClient(connectionString);
        _processor = _client.CreateProcessor(
            topicName,
            subscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4,
                PrefetchCount = 10
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;
        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var integrationEvent = args.Message.Body.ToObjectFromJson<OrderCreatedEvent>()
                ?? throw new InvalidOperationException("Invalid OrderCreatedEvent body.");

            _logger.LogInformation(
                "Notification sent for order {OrderId}, customer {CustomerId}, amount {Amount}; delivery count {DeliveryCount}",
                integrationEvent.OrderId,
                integrationEvent.CustomerId,
                integrationEvent.TotalAmount,
                args.Message.DeliveryCount);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (System.Text.Json.JsonException exception)
        {
            _logger.LogError(exception, "Invalid event JSON; dead-lettering message {MessageId}", args.Message.MessageId);
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidJson",
                exception.Message,
                args.CancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Notification processing failed; abandoning message {MessageId}", args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus error. Entity={EntityPath}, Source={ErrorSource}, Namespace={Namespace}",
            args.EntityPath,
            args.ErrorSource,
            args.FullyQualifiedNamespace);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await _client.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
