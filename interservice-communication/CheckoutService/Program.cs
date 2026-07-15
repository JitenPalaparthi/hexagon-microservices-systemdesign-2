using Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(sp => new ServiceBusPublisher(sp.GetRequiredService<IConfiguration>()));

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "checkout-service" }));

app.MapPost("/orders/asynchronous", async (
    CreateCheckoutRequest request,
    ServiceBusPublisher publisher,
    CancellationToken cancellationToken) =>
{
    if (request.Items.Count == 0) return Results.BadRequest(new { message = "At least one item is required." });

    var orderId = Guid.NewGuid();
    var integrationEvent = new OrderCreatedEvent(
        Guid.NewGuid(),
        orderId,
        request.CustomerId,
        request.Items,
        request.Items.Sum(x => x.UnitPrice * x.Quantity),
        DateTimeOffset.UtcNow);

    await publisher.PublishAsync(integrationEvent, cancellationToken);

    return Results.Accepted($"/orders/{orderId}", new
    {
        orderId,
        status = "Accepted",
        message = "OrderCreated event published. Consumers process it asynchronously."
    });
});

app.Run();
public sealed record CreateCheckoutRequest(Guid CustomerId, IReadOnlyList<OrderItem> Items);
