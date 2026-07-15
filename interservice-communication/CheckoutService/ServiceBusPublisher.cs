using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Contracts;

public sealed class ServiceBusPublisher : IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusPublisher(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");

        var topicName = configuration["ServiceBus:OrdersTopic"] ?? "orders-topic";

        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(topicName);
    }

    public async Task PublishAsync(
        OrderCreatedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(
            BinaryData.FromString(JsonSerializer.Serialize(integrationEvent)))
        {
            MessageId = integrationEvent.EventId.ToString(),
            CorrelationId = integrationEvent.OrderId.ToString(),
            Subject = nameof(OrderCreatedEvent),
            ContentType = "application/json"
        };

        message.ApplicationProperties["event-type"] = nameof(OrderCreatedEvent);
        message.ApplicationProperties["event-version"] = integrationEvent.Version;

        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
