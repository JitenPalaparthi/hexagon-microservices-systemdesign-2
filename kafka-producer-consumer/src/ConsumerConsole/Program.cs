using Confluent.Kafka;

var bootstrap = GetArg("--bootstrap") ?? "localhost:19092,localhost:29092,localhost:39092";
var topic = GetArg("--topic") ?? "orders.console.v1";
var groupId = GetArg("--group") ?? "orders-console-group-1";
var fromBeginning = !string.Equals(GetArg("--from-beginning"), "false", StringComparison.OrdinalIgnoreCase);
var clientId = $"consumer-console-{Environment.MachineName}-{Guid.NewGuid():N}";

var config = new ConsumerConfig
{
    BootstrapServers = bootstrap,
    GroupId = groupId,
    AutoOffsetReset = fromBeginning ? AutoOffsetReset.Earliest : AutoOffsetReset.Latest,
    EnableAutoCommit = false,
    EnableAutoOffsetStore = false,
    ClientId = clientId
};

using var consumer = new ConsumerBuilder<string, string>(config)
    .SetPartitionsAssignedHandler((_, partitions) =>
    {
        Console.WriteLine();
        Console.WriteLine("================ PARTITIONS ASSIGNED ================");
        Console.WriteLine($"Consumer Group : {groupId}");
        Console.WriteLine($"Consumer ID    : {clientId}");
        foreach (var partition in partitions)
        {
            Console.WriteLine($"Topic          : {partition.Topic}");
            Console.WriteLine($"Partition      : {partition.Partition.Value}");
        }
        Console.WriteLine("=====================================================");
    })
    .SetPartitionsRevokedHandler((_, partitions) =>
    {
        Console.WriteLine();
        Console.WriteLine("================ PARTITIONS REVOKED =================");
        Console.WriteLine($"Consumer Group : {groupId}");
        foreach (var partition in partitions)
        {
            Console.WriteLine($"Topic          : {partition.Topic}");
            Console.WriteLine($"Partition      : {partition.Partition.Value}");
            Console.WriteLine($"Offset         : {partition.Offset.Value}");
        }
        Console.WriteLine("=====================================================");
    })
    .SetErrorHandler((_, error) => Console.Error.WriteLine($"Kafka error: {error.Reason}"))
    .Build();

consumer.Subscribe(topic);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("================= CONSUMER STARTED ==================");
Console.WriteLine($"Consumer Group : {groupId}");
Console.WriteLine($"Consumer ID    : {clientId}");
Console.WriteLine($"Topic          : {topic}");
Console.WriteLine($"Bootstrap      : {bootstrap}");
Console.WriteLine($"From Beginning : {fromBeginning}");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine("=====================================================");

try
{
    while (!cts.IsCancellationRequested)
    {
        var result = consumer.Consume(cts.Token);

        Console.WriteLine();
        Console.WriteLine("================ MESSAGE CONSUMED ===================");
        Console.WriteLine($"Consumer Group : {groupId}");
        Console.WriteLine($"Consumer ID    : {clientId}");
        Console.WriteLine($"Topic          : {result.Topic}");
        Console.WriteLine($"Partition      : {result.Partition.Value}");
        Console.WriteLine($"Offset         : {result.Offset.Value}");
        Console.WriteLine($"Key            : {result.Message.Key ?? "<null>"}");
        Console.WriteLine($"Timestamp UTC  : {result.Message.Timestamp.UtcDateTime:O}");
        Console.WriteLine("Value:");
        Console.WriteLine(result.Message.Value);

        consumer.StoreOffset(result);
        consumer.Commit(result);

        Console.WriteLine("Commit Status  : SUCCESS");
        Console.WriteLine("=====================================================");
    }
}
catch (OperationCanceledException)
{
    // Expected when Ctrl+C is pressed.
}
finally
{
    consumer.Close();
    Console.WriteLine($"Consumer '{clientId}' from group '{groupId}' stopped cleanly.");
}

string? GetArg(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
