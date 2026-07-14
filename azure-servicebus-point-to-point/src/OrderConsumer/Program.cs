using System.Text.Json;
using Azure.Messaging.ServiceBus;

const string defaultConnectionString =
    "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;" +
    "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

string connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
    ?? defaultConnectionString;
string queueName = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE") ?? "orders";
string consumerName = Environment.GetEnvironmentVariable("CONSUMER_NAME")
    ?? Environment.MachineName;

await using var client = new ServiceBusClient(connectionString, new ServiceBusClientOptions
{
    TransportType = ServiceBusTransportType.AmqpTcp,
    RetryOptions = new ServiceBusRetryOptions
    {
        Mode = ServiceBusRetryMode.Exponential,
        MaxRetries = 10,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        TryTimeout = TimeSpan.FromSeconds(30)
    }
});

await using ServiceBusProcessor processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
{
    ReceiveMode = ServiceBusReceiveMode.PeekLock,
    AutoCompleteMessages = false,
    MaxConcurrentCalls = 1,
    PrefetchCount = 0,
    MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
});

processor.ProcessMessageAsync += ProcessMessageAsync;
processor.ProcessErrorAsync += ProcessErrorAsync;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine($"OrderConsumer '{consumerName}' started. Queue: {queueName}");
Console.WriteLine("Receive mode: PeekLock. Press Ctrl+C to stop.\n");

await processor.StartProcessingAsync(shutdown.Token);
try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}

await processor.StopProcessingAsync();
Console.WriteLine($"Consumer '{consumerName}' stopped.");

async Task ProcessMessageAsync(ProcessMessageEventArgs args)
{
    try
    {
        OrderMessage? order = args.Message.Body.ToObjectFromJson<OrderMessage>();
        if (order is null)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidPayload",
                "Message body could not be deserialized as OrderMessage.");
            return;
        }

        Console.WriteLine(
            $"[{DateTimeOffset.Now:HH:mm:ss}] RECEIVED by={consumerName} " +
            $"seq={order.Sequence} id={order.OrderId} product={order.Product} " +
            $"qty={order.Quantity} deliveryCount={args.Message.DeliveryCount}");

        // Simulate business processing.
        await Task.Delay(Random.Shared.Next(300, 1000), args.CancellationToken);

        // Explicit settlement: removes the message from the queue after success.
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] COMPLETED seq={order.Sequence}\n");
    }
    catch (JsonException ex)
    {
        await args.DeadLetterMessageAsync(args.Message, "JsonError", ex.Message);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Processing failed: {ex.Message}");
        // Abandon makes the message available for redelivery.
        await args.AbandonMessageAsync(args.Message);
    }
}

Task ProcessErrorAsync(ProcessErrorEventArgs args)
{
    Console.Error.WriteLine(
        $"Service Bus error. Source={args.ErrorSource}, Entity={args.EntityPath}, " +
        $"Namespace={args.FullyQualifiedNamespace}, Exception={args.Exception.Message}");
    return Task.CompletedTask;
}

internal sealed record OrderMessage(
    Guid OrderId,
    int Sequence,
    string Product,
    int Quantity,
    DateTimeOffset CreatedAtUtc);
