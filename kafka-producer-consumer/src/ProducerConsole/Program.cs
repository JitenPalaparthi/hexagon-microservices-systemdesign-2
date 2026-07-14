using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

var bootstrap = GetArg("--bootstrap") ?? "localhost:19092,localhost:29092,localhost:39092";
var topic = GetArg("--topic") ?? "orders.console.v1";
var partitions = int.TryParse(GetArg("--partitions"), out var p) ? p : 6;
var replication = short.TryParse(GetArg("--replication"), out var r) ? r : (short)3;
var intervalMs = int.TryParse(GetArg("--interval-ms"), out var i) ? i : 1000;
var count = int.TryParse(GetArg("--count"), out var c) ? c : 0; // 0 = continuous

if (partitions < 1) throw new ArgumentOutOfRangeException(nameof(partitions));
if (replication is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(replication));
if (intervalMs < 50) throw new ArgumentOutOfRangeException(nameof(intervalMs));

using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build())
{
    try
    {
        await admin.CreateTopicsAsync([new TopicSpecification
        {
            Name = topic,
            NumPartitions = partitions,
            ReplicationFactor = replication
        }]);
        Console.WriteLine($"Created topic '{topic}' with {partitions} partitions and replication factor {replication}.");
    }
    catch (CreateTopicsException ex) when (ex.Results.All(x => x.Error.Code == ErrorCode.TopicAlreadyExists))
    {
        Console.WriteLine($"Topic '{topic}' already exists.");
    }
}

var config = new ProducerConfig
{
    BootstrapServers = bootstrap,
    Acks = Acks.All,
    EnableIdempotence = true,
    MessageSendMaxRetries = 10,
    RetryBackoffMs = 500,
    ClientId = $"producer-console-{Environment.MachineName}"
};

using var producer = new ProducerBuilder<string, string>(config).Build();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"Publishing to '{topic}' every {intervalMs} ms. Press Ctrl+C to stop.");
var random = new Random();
var sent = 0;

try
{
    while (!cts.IsCancellationRequested && (count == 0 || sent < count))
    {
        var payload = new
        {
            id = Guid.NewGuid(),
            orderNumber = $"ORD-{random.Next(100000, 999999)}",
            customerId = random.Next(1, 1000),
            amount = Math.Round(random.NextDouble() * 10000, 2),
            status = new[] { "CREATED", "PAID", "PACKED", "SHIPPED" }[random.Next(4)],
            createdAtUtc = DateTimeOffset.UtcNow
        };

        var key = $"customer-{payload.customerId}";
        var result = await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = JsonSerializer.Serialize(payload)
        }, cts.Token);

        sent++;
        Console.WriteLine($"[{sent}] {result.Topic}[{result.Partition}]@{result.Offset} key={key} value={JsonSerializer.Serialize(payload)}");
        await Task.Delay(intervalMs, cts.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    producer.Flush(TimeSpan.FromSeconds(10));
    Console.WriteLine($"Producer stopped. Messages sent: {sent}");
}

string? GetArg(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
