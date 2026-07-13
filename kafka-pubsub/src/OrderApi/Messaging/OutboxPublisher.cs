using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderApi.Options;

namespace OrderApi.Messaging;

public sealed class OutboxPublisher(
    NpgsqlDataSource dataSource,
    IProducer<string, string> producer,
    IOptions<KafkaOptions> options,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private readonly KafkaOptions _kafka = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishBatchAsync(stoppingToken);
                if (published == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publication cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string selectSql = """
            SELECT event_id, aggregate_id, payload
            FROM outbox_messages
            WHERE published_at_utc IS NULL
            ORDER BY occurred_at_utc
            LIMIT 20
            FOR UPDATE SKIP LOCKED;
            """;

        var messages = new List<(Guid EventId, Guid AggregateId, string Payload)>();
        await using (var select = new NpgsqlCommand(selectSql, connection, transaction))
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                messages.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
            }
        }

        foreach (var message in messages)
        {
            var headers = new Headers
            {
                { "event-id", System.Text.Encoding.UTF8.GetBytes(message.EventId.ToString()) },
                { "event-type", System.Text.Encoding.UTF8.GetBytes("OrderCreatedEvent") }
            };

            var result = await producer.ProduceAsync(
                _kafka.Topic,
                new Message<string, string>
                {
                    Key = message.AggregateId.ToString(),
                    Value = message.Payload,
                    Headers = headers
                },
                cancellationToken);

            const string updateSql = """
                UPDATE outbox_messages
                SET published_at_utc = NOW(), kafka_partition = @partition, kafka_offset = @offset
                WHERE event_id = @event_id;
                """;

            await using var update = new NpgsqlCommand(updateSql, connection, transaction);
            update.Parameters.AddWithValue("partition", result.Partition.Value);
            update.Parameters.AddWithValue("offset", result.Offset.Value);
            update.Parameters.AddWithValue("event_id", message.EventId);
            await update.ExecuteNonQueryAsync(cancellationToken);

            logger.LogInformation(
                "Published event {EventId} for order {OrderId} to {TopicPartitionOffset}",
                message.EventId,
                message.AggregateId,
                result.TopicPartitionOffset);
        }

        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }
}
