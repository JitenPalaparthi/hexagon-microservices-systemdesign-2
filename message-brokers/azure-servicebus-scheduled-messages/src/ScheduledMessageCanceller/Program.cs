using Azure.Messaging.ServiceBus;

var connectionString = RequiredEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");
var queueName = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE_NAME") ?? "scheduled-orders";
var scheduleDelaySeconds = PositiveInteger("SCHEDULE_DELAY_SECONDS", 30);
var cancelAfterSeconds = PositiveInteger("CANCEL_AFTER_SECONDS", 5);

if (cancelAfterSeconds >= scheduleDelaySeconds)
{
    throw new InvalidOperationException(
        "CANCEL_AFTER_SECONDS must be smaller than SCHEDULE_DELAY_SECONDS, otherwise the message may already be available.");
}

await using var client = new ServiceBusClient(
    connectionString,
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp
    });

ServiceBusSender sender = client.CreateSender(queueName);

await ExecuteWithRetryAsync(async () =>
{
    var scheduledUtc = DateTimeOffset.UtcNow.AddSeconds(scheduleDelaySeconds);

    var message = new ServiceBusMessage(
        $"This message should be cancelled before delivery. Created at {DateTimeOffset.UtcNow:O}")
    {
        MessageId = Guid.NewGuid().ToString(),
        Subject = "CancellationDemo",
        ContentType = "text/plain"
    };

    long sequenceNumber = await sender.ScheduleMessageAsync(message, scheduledUtc);

    Console.WriteLine($"Scheduled message {message.MessageId}");
    Console.WriteLine($"Sequence number: {sequenceNumber}");
    Console.WriteLine($"Scheduled UTC: {scheduledUtc:O}");
    Console.WriteLine($"Waiting {cancelAfterSeconds} seconds before cancellation...");

    await Task.Delay(TimeSpan.FromSeconds(cancelAfterSeconds));

    await sender.CancelScheduledMessageAsync(sequenceNumber);

    Console.WriteLine($"Cancelled sequence number {sequenceNumber} at {DateTimeOffset.UtcNow:O}.");
    Console.WriteLine("The consumer should never receive this message.");
});

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
