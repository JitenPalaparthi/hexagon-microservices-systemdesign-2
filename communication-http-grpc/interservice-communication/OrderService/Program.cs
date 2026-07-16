var builder = WebApplication.CreateBuilder(args);
var productUrl = builder.Configuration["Services:ProductService"] ?? "http://localhost:5001";

builder.Services.AddHttpClient<ProductClient>(client =>
{
    client.BaseAddress = new Uri(productUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "order-service" }));

app.MapPost("/orders/synchronous", async (
    CreateOrderRequest request,
    ProductClient products,
    CancellationToken cancellationToken) =>
{
    if (request.Quantity <= 0) return Results.BadRequest(new { message = "Quantity must be greater than zero." });

    try
    {
        var product = await products.GetAsync(request.ProductId, cancellationToken);
        if (product is null) return Results.NotFound(new { message = "Product not found." });
        if (product.AvailableQuantity < request.Quantity)
            return Results.Conflict(new { message = "Insufficient inventory.", product.AvailableQuantity });

        var order = new
        {
            orderId = Guid.NewGuid(),
            product.Id,
            product.Name,
            request.Quantity,
            totalAmount = product.Price * request.Quantity,
            status = "CreatedSynchronously"
        };
        return Results.Created($"/orders/{order.orderId}", order);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem("Product Service timed out.", statusCode: 504);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503, title: "Product Service unavailable");
    }
});

app.Run();
public sealed record CreateOrderRequest(int ProductId, int Quantity);
