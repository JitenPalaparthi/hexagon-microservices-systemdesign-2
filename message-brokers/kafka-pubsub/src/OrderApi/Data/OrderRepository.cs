using System.Text.Json;
using Contracts;
using Npgsql;
using OrderApi.Models;

namespace OrderApi.Data;

public sealed class OrderRepository(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var integrationEvent = new OrderCreatedEvent(
            eventId,
            orderId,
            request.CustomerName.Trim(),
            request.Product.Trim(),
            request.Quantity,
            createdAt);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertOrder = """
            INSERT INTO orders (id, customer_name, product, quantity, created_at_utc)
            VALUES (@id, @customer_name, @product, @quantity, @created_at_utc);
            """;

        await using (var command = new NpgsqlCommand(insertOrder, connection, transaction))
        {
            command.Parameters.AddWithValue("id", orderId);
            command.Parameters.AddWithValue("customer_name", integrationEvent.CustomerName);
            command.Parameters.AddWithValue("product", integrationEvent.Product);
            command.Parameters.AddWithValue("quantity", integrationEvent.Quantity);
            command.Parameters.AddWithValue("created_at_utc", createdAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertOutbox = """
            INSERT INTO outbox_messages (event_id, aggregate_id, event_type, payload, occurred_at_utc)
            VALUES (@event_id, @aggregate_id, @event_type, CAST(@payload AS jsonb), @occurred_at_utc);
            """;

        await using (var command = new NpgsqlCommand(insertOutbox, connection, transaction))
        {
            command.Parameters.AddWithValue("event_id", eventId);
            command.Parameters.AddWithValue("aggregate_id", orderId);
            command.Parameters.AddWithValue("event_type", nameof(OrderCreatedEvent));
            command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(integrationEvent, JsonOptions));
            command.Parameters.AddWithValue("occurred_at_utc", createdAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new OrderResponse(
            orderId,
            integrationEvent.CustomerName,
            integrationEvent.Product,
            integrationEvent.Quantity,
            createdAt,
            "PendingPublication");
    }

    public async Task<OrderResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT o.id, o.customer_name, o.product, o.quantity, o.created_at_utc,
                   CASE WHEN om.published_at_utc IS NULL THEN 'PendingPublication' ELSE 'Published' END AS event_status
            FROM orders o
            JOIN outbox_messages om ON om.aggregate_id = o.id
            WHERE o.id = @id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OrderResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5));
    }
}
