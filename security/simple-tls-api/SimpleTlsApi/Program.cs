var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/hello", () =>
{
    return Results.Ok(new
    {
        message = "Hello from the .NET REST API",
        protocol = "HTTP or HTTPS",
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/products/{id:int}", (int id) =>
{
    return Results.Ok(new
    {
        id,
        name = $"Product-{id}",
        price = 1500.00m,
        available = true
    });
});

app.Run();