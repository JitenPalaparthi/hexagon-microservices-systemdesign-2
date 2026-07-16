namespace ProductRestServer.Models;

public sealed record CreateProductRequest(
    string Name,
    string Description,
    string Category,
    decimal Price,
    int AvailableQuantity);

public sealed record UpdatePriceRequest(decimal NewPrice, string? Reason);
