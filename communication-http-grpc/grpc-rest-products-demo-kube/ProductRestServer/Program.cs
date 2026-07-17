using Microsoft.EntityFrameworkCore;
using ProductRestServer.Data;
using ProductRestServer.Entities;
using ProductRestServer.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

builder.Services.AddDbContext<ProductDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

var instance = Environment.GetEnvironmentVariable("INSTANCE_NAME") ?? Environment.MachineName;

app.MapGet("/", () => Results.Ok(new
{
    service = "product-rest-server",
    instance,
    endpoints = new[] { "GET /api/products", "GET /api/products/{id}", "POST /api/products", "PATCH /api/products/{id}/price", "DELETE /api/products/{id}" }
}));

app.MapGet("/health", async (ProductDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok", database = "postgresql", instance })
        : Results.Problem("PostgreSQL is unavailable.", statusCode: 503));

var products = app.MapGroup("/api/products");

products.MapGet("/", async (string? category, ProductDbContext db, HttpResponse response, CancellationToken ct) =>
{
    response.Headers["X-Backend-Instance"] = instance;
    var query = db.Products.AsNoTracking().OrderBy(x => x.Id).AsQueryable();
    if (!string.IsNullOrWhiteSpace(category))
    {
        var value = category.Trim();
        query = query.Where(x => x.Category == value);
    }
    return Results.Ok(await query.ToListAsync(ct));
});

products.MapGet("/{id:int}", async (int id, ProductDbContext db, HttpResponse response, CancellationToken ct) =>
{
    response.Headers["X-Backend-Instance"] = instance;
    var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    return product is null ? Results.NotFound(new { message = $"Product {id} was not found." }) : Results.Ok(product);
});

products.MapPost("/", async (CreateProductRequest request, ProductDbContext db, HttpResponse response, CancellationToken ct) =>
{
    var errors = Validate(request);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var product = new ProductEntity
    {
        Name = request.Name.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        Category = request.Category.Trim(),
        Price = request.Price,
        AvailableQuantity = request.AvailableQuantity,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    db.Products.Add(product);
    await db.SaveChangesAsync(ct);
    response.Headers["X-Backend-Instance"] = instance;
    return Results.Created($"/api/products/{product.Id}", product);
});

products.MapPatch("/{id:int}/price", async (int id, UpdatePriceRequest request, ProductDbContext db, HttpResponse response, CancellationToken ct) =>
{
    if (request.NewPrice <= 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPrice"] = ["New price must be greater than zero."] });

    var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (product is null) return Results.NotFound(new { message = $"Product {id} was not found." });

    var previousPrice = product.Price;
    product.Price = request.NewPrice;
    await db.SaveChangesAsync(ct);
    response.Headers["X-Backend-Instance"] = instance;
    return Results.Ok(new { product.Id, product.Name, previousPrice, newPrice = product.Price, request.Reason });
});

products.MapDelete("/{id:int}", async (int id, ProductDbContext db, HttpResponse response, CancellationToken ct) =>
{
    var affected = await db.Products.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    response.Headers["X-Backend-Instance"] = instance;
    return affected == 0 ? Results.NotFound(new { message = $"Product {id} was not found." }) : Results.NoContent();
});

app.Run();

static Dictionary<string, string[]> Validate(CreateProductRequest request)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Name)) errors["name"] = ["Name is required."];
    if (string.IsNullOrWhiteSpace(request.Category)) errors["category"] = ["Category is required."];
    if (request.Price <= 0) errors["price"] = ["Price must be greater than zero."];
    if (request.AvailableQuantity < 0) errors["availableQuantity"] = ["Available quantity cannot be negative."];
    return errors;
}
