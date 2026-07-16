using System.Text.Json;
using System.Transactions;
using Azure.Messaging.ServiceBus;

const string OrdersQueue = "orders";
const string BillingQueue = "billing";

string command = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "help";
string connectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION_STRING")
    ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

var options = new ServiceBusClientOptions
{
    EnableCrossEntityTransactions = true,
    RetryOptions = new ServiceBusRetryOptions
    {
        Mode = ServiceBusRetryMode.Exponential,
        MaxRetries = 8,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(5),
        TryTimeout = TimeSpan.FromSeconds(15)
    }
};

await using var client = new ServiceBusClient(connectionString, options);
await using ServiceBusSender ordersSender = client.CreateSender(OrdersQueue);
await using ServiceBusSender billingSender = client.CreateSender(BillingQueue);
await using ServiceBusReceiver ordersReceiver = client.CreateReceiver(
    OrdersQueue,
    new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
await using ServiceBusReceiver billingReceiver = client.CreateReceiver(
    BillingQueue,
    new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

try
{
    switch (command)
    {
        case "wait":
            await WaitUntilReadyAsync(ordersSender);
            Console.WriteLine("Service Bus emulator is ready.");
            break;

        case "reset":
            await WaitUntilReadyAsync(ordersSender);
            await DrainAsync(ordersReceiver, OrdersQueue);
            await DrainAsync(billingReceiver, BillingQueue);
            await PrintStateAsync(ordersReceiver, billingReceiver);
            break;

        case "seed":
            await WaitUntilReadyAsync(ordersSender);
            await SendOrderAsync(ordersSender);
            await PrintStateAsync(ordersReceiver, billingReceiver);
            break;

        case "success":
            await WaitUntilReadyAsync(ordersSender);
            await ProcessOrderInTransactionAsync(
                ordersReceiver,
                billingSender,
                simulateFailure: false);
            await PrintStateAsync(ordersReceiver, billingReceiver);
            break;

        case "failure":
            await WaitUntilReadyAsync(ordersSender);
            await ProcessOrderInTransactionAsync(
                ordersReceiver,
                billingSender,
                simulateFailure: true);
            await PrintStateAsync(ordersReceiver, billingReceiver);
            break;

        case "inspect":
            await WaitUntilReadyAsync(ordersSender);
            await PrintStateAsync(ordersReceiver, billingReceiver);
            break;

        default:
            PrintHelp();
            break;
    }
}
catch (ServiceBusException ex)
{
    Console.Error.WriteLine($"Service Bus error: {ex.Reason}: {ex.Message}");
    Environment.ExitCode = 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    Environment.ExitCode = 1;
}

static async Task WaitUntilReadyAsync(ServiceBusSender sender)
{
    for (int attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            using ServiceBusMessageBatch batch = await sender.CreateMessageBatchAsync();
            return;
        }
        catch when (attempt < 30)
        {
            Console.WriteLine($"Waiting for emulator... attempt {attempt}/30");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

static async Task SendOrderAsync(ServiceBusSender sender)
{
    var order = new
    {
        OrderId = Guid.NewGuid(),
        Customer = "JP",
        Amount = 2499.00m,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(order))
    {
        MessageId = order.OrderId.ToString(),
        Subject = "OrderCreated",
        ContentType = "application/json",
        CorrelationId = $"order-{order.OrderId:N}"
    };

    await sender.SendMessageAsync(message);
    Console.WriteLine($"Seeded order {order.OrderId} into 'orders'.");
}

static async Task ProcessOrderInTransactionAsync(
    ServiceBusReceiver ordersReceiver,
    ServiceBusSender billingSender,
    bool simulateFailure)
{
    ServiceBusReceivedMessage? order = await ordersReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));

    if (order is null)
    {
        Console.WriteLine("No order is available. Run the 'seed' command first.");
        return;
    }

    Console.WriteLine($"Received order MessageId={order.MessageId}, DeliveryCount={order.DeliveryCount}.");

    try
    {
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions
            {
                IsolationLevel = IsolationLevel.Serializable,
                Timeout = TimeSpan.FromSeconds(30)
            },
            TransactionScopeAsyncFlowOption.Enabled);

        var billingEvent = new
        {
            OrderMessageId = order.MessageId,
            Amount = ReadAmount(order.Body),
            Status = "PaymentRequested",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var billingMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(billingEvent))
        {
            MessageId = $"billing-{order.MessageId}",
            CorrelationId = order.CorrelationId,
            Subject = "PaymentRequested",
            ContentType = "application/json"
        };

        await billingSender.SendMessageAsync(billingMessage);
        Console.WriteLine("Transactional operation 1: billing message staged.");

        await ordersReceiver.CompleteMessageAsync(order);
        Console.WriteLine("Transactional operation 2: order completion staged.");

        if (simulateFailure)
        {
            Console.WriteLine("Simulating failure before TransactionScope.Complete().");
            throw new InvalidOperationException("Deliberate failure for rollback demonstration.");
        }

        scope.Complete();
        Console.WriteLine("SUCCESS: transaction committed.");
        Console.WriteLine("Expected state: orders=0 and billing=1.");
    }
    catch (Exception ex) when (simulateFailure)
    {
        Console.WriteLine($"EXPECTED FAILURE: {ex.Message}");
        Console.WriteLine("TransactionScope disposed without Complete(); both operations were rolled back.");

        // The transaction rollback restores the original lock. Abandon it so it becomes
        // immediately visible and can be retried without waiting for lock expiration.
        try
        {
            await ordersReceiver.AbandonMessageAsync(order);
        }
        catch (ServiceBusException abandonError)
        {
            Console.WriteLine($"Could not abandon immediately ({abandonError.Reason}). The message will reappear after its lock expires.");
        }

        Console.WriteLine("Expected state: orders=1 and billing=0.");
    }
}

static decimal? ReadAmount(BinaryData body)
{
    try
    {
        using JsonDocument document = JsonDocument.Parse(body.ToString());
        return document.RootElement.TryGetProperty("Amount", out JsonElement amount)
            ? amount.GetDecimal()
            : null;
    }
    catch (JsonException)
    {
        return null;
    }
}

static async Task DrainAsync(ServiceBusReceiver receiver, string queueName)
{
    int total = 0;

    while (true)
    {
        IReadOnlyList<ServiceBusReceivedMessage> messages =
            await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(2));

        if (messages.Count == 0)
        {
            break;
        }

        foreach (ServiceBusReceivedMessage message in messages)
        {
            await receiver.CompleteMessageAsync(message);
            total++;
        }
    }

    Console.WriteLine($"Reset '{queueName}': removed {total} message(s).");
}

static async Task PrintStateAsync(
    ServiceBusReceiver ordersReceiver,
    ServiceBusReceiver billingReceiver)
{
    IReadOnlyList<ServiceBusReceivedMessage> orders = await ordersReceiver.PeekMessagesAsync(100);
    IReadOnlyList<ServiceBusReceivedMessage> billing = await billingReceiver.PeekMessagesAsync(100);

    Console.WriteLine();
    Console.WriteLine("Queue state (peeked, up to 100 messages):");
    Console.WriteLine($"  orders : {orders.Count}");
    Console.WriteLine($"  billing: {billing.Count}");

    foreach (ServiceBusReceivedMessage message in orders)
    {
        Console.WriteLine($"  orders -> MessageId={message.MessageId}, Body={message.Body}");
    }

    foreach (ServiceBusReceivedMessage message in billing)
    {
        Console.WriteLine($"  billing -> MessageId={message.MessageId}, Body={message.Body}");
    }

    Console.WriteLine();
}

static void PrintHelp()
{
    Console.WriteLine("Azure Service Bus transaction demo");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  wait     Verify that the emulator accepts AMQP operations");
    Console.WriteLine("  reset    Remove all messages from orders and billing");
    Console.WriteLine("  seed     Add one message to orders");
    Console.WriteLine("  success  Send billing + complete order, then commit atomically");
    Console.WriteLine("  failure  Stage both operations, throw, and roll back atomically");
    Console.WriteLine("  inspect  Peek and print current queue contents");
}
