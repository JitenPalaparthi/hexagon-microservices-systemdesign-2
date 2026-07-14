using System.Text.Json;
using Azure.Messaging.ServiceBus;

const string defaultConnectionString =
    "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;" +
    "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

string connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
    ?? defaultConnectionString;
string queueName = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE") ?? "orders";
int delaySeconds = int.TryParse(Environment.GetEnvironmentVariable("SEND_INTERVAL_SECONDS"), out int delay)
    ? Math.Max(delay, 1)
    : 2;

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
await using ServiceBusSender sender = client.CreateSender(queueName);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine($"OrderProducer started. Queue: {queueName}");
Console.WriteLine($"Sending one order every {delaySeconds} second(s). Press Ctrl+C to stop.\n");

string[] products = ["Laptop", "Keyboard", "Monitor", "Mouse", "Headset", "Webcam"];

int sequence = 0;
while (!shutdown.IsCancellationRequested)
{
    sequence++;
    var order = new OrderMessage(
        OrderId: Guid.NewGuid(),
        Sequence: sequence,
        Product: products[Random.Shared.Next(products.Length)],
        Quantity: Random.Shared.Next(1, 6),
        CreatedAtUtc: DateTimeOffset.UtcNow);

    string json = JsonSerializer.Serialize(order);
    var message = new ServiceBusMessage(json)
    {
        MessageId = order.OrderId.ToString(),
        Subject = "order.created",
        ContentType = "application/json",
        CorrelationId = $"batch-{DateTime.UtcNow:yyyyMMdd}"
    };
    message.ApplicationProperties["sequence"] = order.Sequence;
    message.ApplicationProperties["producer"] = Environment.MachineName;

    try
    {
        await sender.SendMessageAsync(message, shutdown.Token);
        Console.WriteLine(
            $"[{DateTimeOffset.Now:HH:mm:ss}] SENT seq={order.Sequence} " +
            $"id={order.OrderId} product={order.Product} qty={order.Quantity}");
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Send failed: {ex.Message}");
    }

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), shutdown.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

Console.WriteLine("Producer stopped.");


internal sealed record OrderMessage(
    Guid OrderId,
    int Sequence,
    string Product,
    int Quantity,
    DateTimeOffset CreatedAtUtc);
