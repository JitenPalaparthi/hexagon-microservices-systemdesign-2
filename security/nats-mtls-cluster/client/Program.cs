using NATS.Client.Core;

var mode = Environment.GetEnvironmentVariable("MODE") ?? "publish";
var urls = Environment.GetEnvironmentVariable("NATS_URL")
           ?? "tls://localhost:4222,tls://localhost:4223,tls://localhost:4224";
var subject = Environment.GetEnvironmentVariable("NATS_SUBJECT") ?? "orders.created";
var caFile = Environment.GetEnvironmentVariable("NATS_CA_FILE") ?? "../certs/ca/ca.crt";
var certFile = Environment.GetEnvironmentVariable("NATS_CERT_FILE") ?? "../certs/client/client.crt";
var keyFile = Environment.GetEnvironmentVariable("NATS_KEY_FILE") ?? "../certs/client/client.key";

var options = new NatsOpts
{
    Name = $"nats-mtls-{mode}",
    Url = urls,
    TlsOpts = new NatsTlsOpts
    {
        Mode = TlsMode.Require,
        CaFile = caFile,
        CertFile = certFile,
        KeyFile = keyFile,
        InsecureSkipVerify = false
    },
    RetryOnInitialConnect = true,
    MaxReconnectRetry = -1,
    ReconnectWaitMin = TimeSpan.FromSeconds(1),
    ReconnectWaitMax = TimeSpan.FromSeconds(3)
};

await using var connection = new NatsConnection(options);
await connection.ConnectAsync();

Console.WriteLine($"Connected using mTLS to {urls}");

if (mode.Equals("subscribe", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Subscribed to '{subject}'. Press Ctrl+C to stop.");

    await foreach (var message in connection.SubscribeAsync<string>(subject))
    {
        Console.WriteLine(
            $"[{DateTimeOffset.UtcNow:O}] subject={message.Subject} data={message.Data}");
    }

    return;
}

var payload = args.Length > 0
    ? string.Join(' ', args)
    : $"Order created at {DateTimeOffset.UtcNow:O}";

await connection.PublishAsync(subject, payload);

Console.WriteLine($"Published to '{subject}': {payload}");
