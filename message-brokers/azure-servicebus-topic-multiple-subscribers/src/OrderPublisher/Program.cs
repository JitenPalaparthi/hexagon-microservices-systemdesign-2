using Azure.Messaging.ServiceBus;

const string localConnectionString =
    "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;" +
    "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

string connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
    ?? localConnectionString;
string topicName = Environment.GetEnvironmentVariable("SERVICEBUS_TOPIC_NAME") ?? "orders-topic";
int messageCount = ReadPositiveInt("MESSAGE_COUNT", 10);
int intervalMs = ReadNonNegativeInt("SEND_INTERVAL_MS", 700);

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
await using ServiceBusSender sender = client.CreateSender(topicName);

string batchId = $"batch-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
Console.WriteLine("Azure Service Bus topic publisher");
Console.WriteLine($"Topic: {topicName}");
Console.WriteLine($"Messages: {messageCount}");
Console.WriteLine($"Batch: {batchId}\n");

for (int number = 1; number <= messageCount; number++)
{
    var order = new OrderCreated(
        OrderId: Guid.NewGuid(),
        CustomerId: $"customer-{Random.Shared.Next(1000, 9999)}",
        Product: Products.All[Random.Shared.Next(Products.All.Length)],
        Quantity: Random.Shared.Next(1, 6),
        UnitPrice: Math.Round(Random.Shared.NextDouble() * 900 + 100, 2),
        CreatedAtUtc: DateTimeOffset.UtcNow);

    var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(order))
    {
        MessageId = order.OrderId.ToString(),
        Subject = "order.created",
        ContentType = "application/json",
        CorrelationId = batchId
    };
    message.ApplicationProperties["eventType"] = "OrderCreated";
    message.ApplicationProperties["publisher"] = Environment.MachineName;

    await SendWithStartupRetryAsync(sender, message);

    Console.WriteLine(
        $"PUBLISHED {number,2}/{messageCount} | MessageId={message.MessageId} | " +
        $"Product={order.Product} | Qty={order.Quantity} | Total={order.UnitPrice * order.Quantity:F2}");

    if (number < messageCount && intervalMs > 0)
    {
        await Task.Delay(intervalMs);
    }
}

Console.WriteLine("\nPublisher finished. Each subscription receives an independent copy.");

static async Task SendWithStartupRetryAsync(ServiceBusSender sender, ServiceBusMessage message)
{
    const int maxAttempts = 30;
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await sender.SendMessageAsync(message);
            return;
        }
        catch (ServiceBusException exception) when (exception.IsTransient && attempt < maxAttempts)
        {
            Console.WriteLine(
                $"Service Bus is not ready ({attempt}/{maxAttempts}): {exception.Reason}. Retrying...");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

static int ReadPositiveInt(string name, int fallback) =>
    int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0
        ? value
        : fallback;

static int ReadNonNegativeInt(string name, int fallback) =>
    int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value >= 0
        ? value
        : fallback;

internal sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    string Product,
    int Quantity,
    double UnitPrice,
    DateTimeOffset CreatedAtUtc);

internal static class Products
{
    public static readonly string[] All =
    [
        "Mechanical Keyboard",
        "USB-C Dock",
        "Noise-Cancelling Headphones",
        "4K Monitor",
        "Web Camera"
    ];
}
