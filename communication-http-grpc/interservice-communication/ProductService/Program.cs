var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var products = new Dictionary<int, Product>
{
    [101] = new(101, "Laptop", 85_000m, 10),
    [102] = new(102, "Keyboard", 3_500m, 25),
    [103] = new(103, "Mouse", 1_800m, 50)
};

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "product-service" }));

app.MapGet("/products/{id:int}", (int id) =>
    products.TryGetValue(id, out var product)
        ? Results.Ok(product)
        : Results.NotFound(new { message = $"Product {id} was not found." }));

app.Run();

public sealed record Product(int Id, string Name, decimal Price, int AvailableQuantity);
