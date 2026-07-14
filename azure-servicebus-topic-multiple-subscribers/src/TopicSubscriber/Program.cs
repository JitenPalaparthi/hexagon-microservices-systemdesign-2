using Azure.Messaging.ServiceBus;

const string localConnectionString =
    "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;" +
    "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

string connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
    ?? localConnectionString;
string topicName = Environment.GetEnvironmentVariable("SERVICEBUS_TOPIC_NAME") ?? "orders-topic";
string subscriptionName = Environment.GetEnvironmentVariable("SERVICEBUS_SUBSCRIPTION_NAME")
    ?? "inventory-subscription";
string subscriberName = Environment.GetEnvironmentVariable("SUBSCRIBER_NAME")
    ?? subscriptionName;

var options = new ServiceBusClientOptions
{
    TransportType = ServiceBusTransportType.AmqpTcp,
    RetryOptions = new ServiceBusRetryOptions
    {
        Mode = ServiceBusRetryMode.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        MaxRetries = 10,
        TryTimeout = TimeSpan.FromSeconds(30)
    }
};

await using var client = new ServiceBusClient(connectionString, options);
await using ServiceBusProcessor processor = client.CreateProcessor(
    topicName,
    subscriptionName,
    new ServiceBusProcessorOptions
    {
        AutoCompleteMessages = false,
        ReceiveMode = ServiceBusReceiveMode.PeekLock,
        MaxConcurrentCalls = 1,
        PrefetchCount = 0,
        MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
    });

processor.ProcessMessageAsync += ProcessMessageAsync;
processor.ProcessErrorAsync += ProcessErrorAsync;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine("Azure Service Bus topic subscriber");
Console.WriteLine($"Subscriber: {subscriberName}");
Console.WriteLine($"Topic: {topicName}");
Console.WriteLine($"Subscription: {subscriptionName}");
Console.WriteLine("Receive mode: PeekLock\n");

await processor.StartProcessingAsync(shutdown.Token);
try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
}
catch (OperationCanceledException)
{
    // Expected during Ctrl+C or container shutdown.
}
await processor.StopProcessingAsync();

async Task ProcessMessageAsync(ProcessMessageEventArgs args)
{
    try
    {
        OrderCreated? order = args.Message.Body.ToObjectFromJson<OrderCreated>();
        if (order is null)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidPayload",
                "Message body is not a valid OrderCreated event.",
                args.CancellationToken);
            return;
        }

        Console.WriteLine(
            $"RECEIVED | Subscriber={subscriberName} | Subscription={subscriptionName} | " +
            $"Sequence={args.Message.SequenceNumber} | Delivery={args.Message.DeliveryCount}");
        Console.WriteLine(
            $"         MessageId={args.Message.MessageId} | OrderId={order.OrderId} | " +
            $"Product={order.Product} | Qty={order.Quantity} | Total={order.UnitPrice * order.Quantity:F2}");

        await RunSubscriberWorkAsync(subscriberName, order, args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        Console.WriteLine($"COMPLETED | Subscriber={subscriberName} | MessageId={args.Message.MessageId}\n");
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        Console.Error.WriteLine(
            $"FAILED | Subscriber={subscriberName} | MessageId={args.Message.MessageId} | {exception.Message}");
        await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
    }
}

Task ProcessErrorAsync(ProcessErrorEventArgs args)
{
    Console.Error.WriteLine(
        $"SERVICE BUS ERROR | Subscriber={subscriberName} | Source={args.ErrorSource} | " +
        $"Entity={args.EntityPath} | {args.Exception.Message}");
    return Task.CompletedTask;
}

static async Task RunSubscriberWorkAsync(
    string subscriber,
    OrderCreated order,
    CancellationToken cancellationToken)
{
    string activity = subscriber switch
    {
        "inventory-service" => $"Reserving {order.Quantity} unit(s) of {order.Product}",
        "billing-service" => $"Creating invoice for {order.UnitPrice * order.Quantity:F2}",
        "notification-service" => $"Sending order confirmation to {order.CustomerId}",
        _ => "Processing order event"
    };

    Console.WriteLine($"ACTION   | Subscriber={subscriber} | {activity}");
    await Task.Delay(Random.Shared.Next(250, 800), cancellationToken);
}

internal sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    string Product,
    int Quantity,
    double UnitPrice,
    DateTimeOffset CreatedAtUtc);
