using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ObservabilityApi.Models;
using ObservabilityApi.Observability;

namespace ObservabilityApi.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(
    ProductRepository repository,
    ProductTelemetry telemetry,
    ILogger<ProductsController> logger) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<Product>> GetAll()
    {
        using var activity = ProductTelemetry.ActivitySource.StartActivity("products.list", ActivityKind.Internal);
        activity?.SetTag("products.result.count", repository.GetAll().Count);
        return Ok(repository.GetAll());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = ProductTelemetry.ActivitySource.StartActivity("products.get", ActivityKind.Internal);
        activity?.SetTag("product.id", id);

        await Task.Delay(Random.Shared.Next(25, 150), cancellationToken);
        var product = repository.Get(id);
        var found = product is not null;

        activity?.SetTag("product.found", found);
        telemetry.RecordRead(found, stopwatch.Elapsed.TotalMilliseconds);

        if (!found)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Product not found");
            logger.LogWarning("Product {ProductId} was not found. TraceId={TraceId}", id, Activity.Current?.TraceId);
            return NotFound(new { message = $"Product {id} was not found.", traceId = Activity.Current?.TraceId.ToString() });
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0 || request.Stock < 0)
        {
            return ValidationProblem("Name is required, price must be greater than zero, and stock cannot be negative.");
        }

        var stopwatch = Stopwatch.StartNew();
        using var activity = ProductTelemetry.ActivitySource.StartActivity("products.create", ActivityKind.Internal);
        activity?.SetTag("product.name", request.Name);
        activity?.SetTag("product.price", request.Price);

        await Task.Delay(Random.Shared.Next(50, 250), cancellationToken);
        var product = repository.Add(request);

        activity?.SetTag("product.id", product.Id);
        telemetry.RecordCreated(stopwatch.Elapsed.TotalMilliseconds);
        logger.LogInformation("Created product {ProductId}. TraceId={TraceId}", product.Id, Activity.Current?.TraceId);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }
}
