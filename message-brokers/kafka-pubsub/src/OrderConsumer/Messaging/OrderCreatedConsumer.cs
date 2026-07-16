using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderConsumer.Data;
using OrderConsumer.Options;

namespace OrderConsumer.Messaging;

public sealed class OrderCreatedConsumer(
    IOptions<KafkaOptions> options,
    ProcessedOrderRepository repository,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaOptions _kafka = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            GroupId = _kafka.ConsumerGroup,
            ClientId = Environment.MachineName,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => logger.LogError("Kafka error: {Reason}", error.Reason))
            .Build();

        consumer.Subscribe(_kafka.Topic);
        logger.LogInformation("Consumer subscribed to {Topic} as group {Group}", _kafka.Topic, _kafka.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    var message = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value, JsonOptions)
                        ?? throw new JsonException("Kafka payload deserialized to null.");

                    var inserted = repository.TryInsertAsync(message, stoppingToken).GetAwaiter().GetResult();
                    consumer.StoreOffset(result);
                    consumer.Commit(result);

                    if (inserted)
                    {
                        logger.LogInformation(
                            "Processed order {OrderId}, event {EventId}, offset {Offset}",
                            message.OrderId,
                            message.EventId,
                            result.TopicPartitionOffset);
                    }
                    else
                    {
                        logger.LogInformation(
                            "Skipped duplicate event {EventId} at offset {Offset}",
                            message.EventId,
                            result.TopicPartitionOffset);
                    }
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume failure: {Reason}", ex.Error.Reason);
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Invalid message at current Kafka offset; offset is not committed");
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
                catch (NpgsqlException ex)
                {
                    logger.LogError(ex, "Database failure; Kafka offset is not committed");
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Kafka consumer is stopping");
        }
        finally
        {
            consumer.Close();
        }
    }
}
