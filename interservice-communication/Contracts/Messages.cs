namespace Contracts;

public sealed record OrderItem(int ProductId, int Quantity, decimal UnitPrice);

public sealed record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderItem> Items,
    decimal TotalAmount,
    DateTimeOffset OccurredAtUtc,
    int Version = 1);

public sealed record WarehouseAllocationRequest(
    Guid OrderId,
    IReadOnlyList<WarehouseItem> Items);

public sealed record WarehouseItem(int ProductId, int Quantity);

public sealed record WarehouseAllocationResponse(
    Guid AllocationId,
    string Status,
    bool IsReplay);
