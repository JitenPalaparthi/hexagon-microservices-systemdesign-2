namespace Contracts;

public sealed record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    string CustomerName,
    string Product,
    int Quantity,
    DateTimeOffset CreatedAtUtc);
