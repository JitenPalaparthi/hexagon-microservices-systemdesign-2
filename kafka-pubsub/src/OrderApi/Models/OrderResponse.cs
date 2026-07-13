namespace OrderApi.Models;

public sealed record OrderResponse(
    Guid Id,
    string CustomerName,
    string Product,
    int Quantity,
    DateTimeOffset CreatedAtUtc,
    string EventStatus);
