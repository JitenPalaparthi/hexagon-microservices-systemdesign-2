using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "dotnet10-observability-api";
const string serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ProductTelemetry>();
builder.Services.AddSingleton<ProductRepository>();

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddSource(ProductTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = context => context.Request.Path != "/metrics";
        })
        .AddHttpClientInstrumentation(options => options.RecordException = true)
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(
                builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddMeter(ProductTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.Use(async (context, next) =>
{
    await next();
    var traceId = Activity.Current?.TraceId.ToString();
    if (!string.IsNullOrWhiteSpace(traceId) && !context.Response.HasStarted)
    {
        context.Response.Headers["X-Trace-Id"] = traceId;
    }
});

app.MapGet("/", () => Results.Ok(new
{
    service = serviceName,
    version = serviceVersion,
    endpoints = new[]
    {
        "GET /api/products",
        "GET /api/products/{id}",
        "POST /api/products",
        "GET /api/diagnostics/slow?milliseconds=1000",
        "GET /api/diagnostics/error",
        "GET /health",
        "GET /metrics"
    }
}));

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", utc = DateTimeOffset.UtcNow }));

app.MapGet("/api/products", (ProductRepository repository, ProductTelemetry telemetry) =>
{
    using var activity = telemetry.StartActivity("products.list");
    var products = repository.GetAll();
    telemetry.RecordRead("list");
    activity?.SetTag("products.count", products.Count);
    return Results.Ok(products);
});

app.MapGet("/api/products/{id:int}", (int id, ProductRepository repository, ProductTelemetry telemetry) =>
{
    using var activity = telemetry.StartActivity("products.get");
    activity?.SetTag("product.id", id);

    var product = repository.Get(id);
    telemetry.RecordRead("single");

    if (product is null)
    {
        activity?.SetTag("product.found", false);
        return Results.NotFound(new { message = $"Product {id} was not found." });
    }

    activity?.SetTag("product.found", true);
    return Results.Ok(product);
});

app.MapPost("/api/products", (CreateProductRequest request, ProductRepository repository, ProductTelemetry telemetry) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0 || request.Stock < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["product"] = ["Name is required, price must be positive, and stock cannot be negative."]
        });
    }

    using var activity = telemetry.StartActivity("products.create");
    var product = repository.Create(request);
    telemetry.RecordCreated(product.Price);
    activity?.SetTag("product.id", product.Id);
    activity?.SetTag("product.price", product.Price);

    return Results.Created($"/api/products/{product.Id}", product);
});

app.MapGet("/api/diagnostics/slow", async (int? milliseconds, ProductTelemetry telemetry) =>
{
    var delay = Math.Clamp(milliseconds ?? 1000, 50, 10000);
    using var activity = telemetry.StartActivity("diagnostics.slow-operation");
    activity?.SetTag("delay.ms", delay);

    var started = Stopwatch.GetTimestamp();
    await Task.Delay(delay);
    telemetry.RecordOperationDuration(Stopwatch.GetElapsedTime(started).TotalMilliseconds, "slow");

    return Results.Ok(new { delayedMilliseconds = delay, traceId = Activity.Current?.TraceId.ToString() });
});

app.MapGet("/api/diagnostics/error", (ProductTelemetry telemetry) =>
{
    using var activity = telemetry.StartActivity("diagnostics.error");
    var exception = new InvalidOperationException("Intentional demonstration error.");
    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    activity?.AddException(exception);
    telemetry.RecordError("intentional");
    throw exception;
});

app.MapPrometheusScrapingEndpoint();
app.Run();

public sealed record Product(int Id, string Name, decimal Price, int Stock);
public sealed record CreateProductRequest(string Name, decimal Price, int Stock);

public sealed class ProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private int _nextId = 3;

    public ProductRepository()
    {
        _products[1] = new Product(1, "Mechanical Keyboard", 129.99m, 15);
        _products[2] = new Product(2, "USB-C Dock", 89.50m, 22);
    }

    public IReadOnlyCollection<Product> GetAll() => _products.Values.OrderBy(p => p.Id).ToArray();
    public Product? Get(int id) => _products.GetValueOrDefault(id);

    public Product Create(CreateProductRequest request)
    {
        var id = Interlocked.Increment(ref _nextId);
        var product = new Product(id, request.Name.Trim(), request.Price, request.Stock);
        _products[id] = product;
        return product;
    }
}

public sealed class ProductTelemetry : IDisposable
{
    public const string ActivitySourceName = "Demo.Products.Tracing";
    public const string MeterName = "Demo.Products.Metrics";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Meter _meter = new(MeterName, "1.0.0");
    private readonly Counter<long> _reads;
    private readonly Counter<long> _created;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _createdPrice;
    private readonly Histogram<double> _operationDuration;

    public ProductTelemetry()
    {
        _reads = _meter.CreateCounter<long>("products.read.count", unit: "{request}");
        _created = _meter.CreateCounter<long>("products.created.count", unit: "{product}");
        _errors = _meter.CreateCounter<long>("application.errors.count", unit: "{error}");
        _createdPrice = _meter.CreateHistogram<double>("products.created.price", unit: "USD");
        _operationDuration = _meter.CreateHistogram<double>("application.operation.duration", unit: "ms");
    }

    public Activity? StartActivity(string name) => _activitySource.StartActivity(name, ActivityKind.Internal);
    public void RecordRead(string operation) => _reads.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void RecordCreated(decimal price)
    {
        _created.Add(1);
        _createdPrice.Record((double)price);
    }

    public void RecordError(string type) => _errors.Add(1, new KeyValuePair<string, object?>("error.type", type));
    public void RecordOperationDuration(double milliseconds, string operation) =>
        _operationDuration.Record(milliseconds, new KeyValuePair<string, object?>("operation", operation));

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
