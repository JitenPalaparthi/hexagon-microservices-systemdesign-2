using System.Collections.Concurrent;
using Contracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var allocations = new ConcurrentDictionary<string, WarehouseAllocationResponse>();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "warehouse-service" }));

app.MapPost("/warehouse/allocations", (
    WarehouseAllocationRequest request,
    HttpRequest httpRequest) =>
{
    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
        return Results.BadRequest(new { message = "Idempotency-Key header is required." });

    if (allocations.TryGetValue(idempotencyKey, out var existing))
        return Results.Ok(existing with { IsReplay = true });

    var created = new WarehouseAllocationResponse(Guid.NewGuid(), "Allocated", false);
    allocations[idempotencyKey] = created;
    return Results.Ok(created);
});

app.Run();
