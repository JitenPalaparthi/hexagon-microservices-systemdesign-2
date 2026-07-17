namespace ObservabilityApi.Models;

public sealed record Product(int Id, string Name, decimal Price, int Stock);

public sealed record CreateProductRequest(string Name, decimal Price, int Stock);
