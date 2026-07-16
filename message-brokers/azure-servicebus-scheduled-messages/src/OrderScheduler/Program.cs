using System.Text.Json;
using Azure.Messaging.ServiceBus;

var connectionString = RequiredEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");
var queueName = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE_NAME") ?? "scheduled-orders";
var messageCount = PositiveInteger("MESSAGE_COUNT", 3);
var firstDelaySeconds = PositiveInteger("FIRST_DELAY_SECONDS", 10);
var delayBetweenMessagesSeconds = PositiveInteger("DELAY_BETWEEN_MESSAGES_SECONDS", 10);

Console.WriteLine("Azure Service Bus scheduled-message producer");
Console.WriteLine($"Queue: {queueName}");
Console.WriteLine($"Messages: {messageCount}");
Console.WriteLine($"First delay: {firstDelaySeconds} seconds");
Console.WriteLine($"Spacing: {delayBetweenMessagesSeconds} seconds");
Console.WriteLine();

await using var client = new ServiceBusClient(
    connectionString,
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp
    });

ServiceBusSender sender = client.CreateSender(queueName);

await ExecuteWithRetryAsync(async () =>
{
    for (var index = 1; index <= messageCount; index++)
    {
        var delaySeconds = firstDelaySeconds + ((index - 1) * delayBetweenMessagesSeconds);
        var scheduledUtc = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);

        var order = new OrderCreated(
            OrderId: Guid.NewGuid(),
            CustomerId: $"customer-{index:000}",
            Amount: decimal.Round(100m + (index * 25.50m), 2),
            CreatedUtc: DateTimeOffset.UtcNow);

        var body = JsonSerializer.Serialize(order);
        var message = new ServiceBusMessage(body)
        {
            MessageId = order.OrderId.ToString(),
            Subject = "OrderCreated",
            ContentType = "application/json",
            CorrelationId = $"schedule-demo-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
        };

        message.ApplicationProperties["ScheduledBy"] = "OrderScheduler";
        message.ApplicationProperties["RequestedDelaySeconds"] = delaySeconds;

        long sequenceNumber = await sender.ScheduleMessageAsync(message, scheduledUtc);

        Console.WriteLine($"[{DateTimeOffset.UtcNow:O}] Scheduled message");
        Console.WriteLine($"  Order ID:          {order.OrderId}");
        Console.WriteLine($"  Sequence number:   {sequenceNumber}");
        Console.WriteLine($"  Available after:   {scheduledUtc:O}");
        Console.WriteLine($"  Delay:             {delaySeconds} seconds");
        Console.WriteLine();
    }
});

Console.WriteLine("All messages have been scheduled. The producer will now exit.");

static string RequiredEnvironmentVariable(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Required environment variable '{name}' is missing.");

static int PositiveInteger(string name, int fallback)
{
    string? value = Environment.GetEnvironmentVariable(name);
    return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
}

static async Task ExecuteWithRetryAsync(Func<Task> operation)
{
    const int maximumAttempts = 30;

    for (var attempt = 1; attempt <= maximumAttempts; attempt++)
    {
        try
        {
            await operation();
            return;
        }
        catch (ServiceBusException exception) when (exception.IsTransient && attempt < maximumAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 15));
            Console.WriteLine(
                $"Service Bus is not ready. Attempt {attempt}/{maximumAttempts}; retrying in {delay.TotalSeconds} seconds. " +
                $"Reason: {exception.Reason}");
            await Task.Delay(delay);
        }
    }
}

internal sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset CreatedUtc);
