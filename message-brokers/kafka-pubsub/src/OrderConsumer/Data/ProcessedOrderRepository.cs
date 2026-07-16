using Contracts;
using Npgsql;

namespace OrderConsumer.Data;

public sealed class ProcessedOrderRepository(NpgsqlDataSource dataSource)
{
    public async Task<bool> TryInsertAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO processed_orders
                (event_id, order_id, customer_name, product, quantity, order_created_at_utc, processed_at_utc)
            VALUES
                (@event_id, @order_id, @customer_name, @product, @quantity, @order_created_at_utc, NOW())
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("event_id", message.EventId);
        command.Parameters.AddWithValue("order_id", message.OrderId);
        command.Parameters.AddWithValue("customer_name", message.CustomerName);
        command.Parameters.AddWithValue("product", message.Product);
        command.Parameters.AddWithValue("quantity", message.Quantity);
        command.Parameters.AddWithValue("order_created_at_utc", message.CreatedAtUtc);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
