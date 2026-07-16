using Azure.Messaging.ServiceBus;

var connectionString = RequiredEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");
var queueName = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE_NAME") ?? "scheduled-orders";
var consumerName = Environment.GetEnvironmentVariable("CONSUMER_NAME") ?? "consumer-1";
var maxConcurrentCalls = PositiveInteger("MAX_CONCURRENT_CALLS", 1);
var prefetchCount = NonNegativeInteger("PREFETCH_COUNT", 0);

await using var client = new ServiceBusClient(
    connectionString,
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp
    });

var processorOptions = new ServiceBusProcessorOptions
{
    AutoCompleteMessages = false,
    MaxConcurrentCalls = maxConcurrentCalls,
    PrefetchCount = prefetchCount,
    ReceiveMode = ServiceBusReceiveMode.PeekLock,
    MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
};

ServiceBusProcessor processor = client.CreateProcessor(queueName, processorOptions);

processor.ProcessMessageAsync += async args =>
{
    ServiceBusReceivedMessage message = args.Message;

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine($"Consumer:              {consumerName}");
    Console.WriteLine($"Received UTC:          {DateTimeOffset.UtcNow:O}");
    Console.WriteLine($"Message ID:            {message.MessageId}");
    Console.WriteLine($"Sequence number:       {message.SequenceNumber}");
    Console.WriteLine($"Scheduled enqueue UTC: {message.ScheduledEnqueueTime:O}");
    Console.WriteLine($"Actual enqueue UTC:    {message.EnqueuedTime:O}");
    Console.WriteLine($"Delivery count:        {message.DeliveryCount}");
    Console.WriteLine($"Subject:               {message.Subject}");
    Console.WriteLine($"Body:                  {message.Body}");

    await Task.Delay(500, args.CancellationToken);
    await args.CompleteMessageAsync(message, args.CancellationToken);

    Console.WriteLine("Result:                 COMPLETED");
    Console.WriteLine("============================================================");
};

processor.ProcessErrorAsync += args =>
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Service Bus processor error");
    Console.Error.WriteLine($"Source:    {args.ErrorSource}");
    Console.Error.WriteLine($"Entity:    {args.EntityPath}");
    Console.Error.WriteLine($"Namespace: {args.FullyQualifiedNamespace}");
    Console.Error.WriteLine(args.Exception);
    return Task.CompletedTask;
};

Console.WriteLine("Azure Service Bus scheduled-message consumer");
Console.WriteLine($"Consumer: {consumerName}");
Console.WriteLine($"Queue: {queueName}");
Console.WriteLine("The consumer is running now, but scheduled messages are invisible until their scheduled enqueue time.");
Console.WriteLine("Press Ctrl+C to stop.");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await StartProcessorWithRetryAsync(processor, shutdown.Token);

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
finally
{
    await processor.StopProcessingAsync();
}

static string RequiredEnvironmentVariable(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Required environment variable '{name}' is missing.");

static int PositiveInteger(string name, int fallback)
{
    string? value = Environment.GetEnvironmentVariable(name);
    return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
}

static int NonNegativeInteger(string name, int fallback)
{
    string? value = Environment.GetEnvironmentVariable(name);
    return int.TryParse(value, out int parsed) && parsed >= 0 ? parsed : fallback;
}

static async Task StartProcessorWithRetryAsync(
    ServiceBusProcessor processor,
    CancellationToken cancellationToken)
{
    const int maximumAttempts = 30;

    for (var attempt = 1; attempt <= maximumAttempts; attempt++)
    {
        try
        {
            await processor.StartProcessingAsync(cancellationToken);
            return;
        }
        catch (ServiceBusException exception) when (
            exception.IsTransient &&
            attempt < maximumAttempts &&
            !cancellationToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 15));
            Console.WriteLine(
                $"Service Bus is not ready. Attempt {attempt}/{maximumAttempts}; retrying in {delay.TotalSeconds} seconds. " +
                $"Reason: {exception.Reason}");
            await Task.Delay(delay, cancellationToken);
        }
    }
}
