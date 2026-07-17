using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ObservabilityApi.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<ProductTelemetry>();
builder.Services.AddSingleton<ProductRepository>();

var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet10-observability-api";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.namespace"] = "examples"
        }))
    .WithTracing(tracing => tracing
        .AddSource(ProductTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = context => !context.Request.Path.StartsWithSegments("/metrics");
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.header.user_agent", request.Headers.UserAgent.ToString());
            };
        })
        .AddHttpClientInstrumentation(options => options.RecordException = true)
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddMeter(ProductTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Trace-Id"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "none";
    await next();
});

app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");

app.MapGet("/", () => Results.Ok(new
{
    service = serviceName,
    endpoints = new[]
    {
        "GET /api/products",
        "GET /api/products/{id}",
        "POST /api/products",
        "GET /api/diagnostics/slow?milliseconds=750",
        "GET /api/diagnostics/error",
        "GET /health",
        "GET /metrics"
    },
    jaeger = "http://localhost:16686",
    prometheus = "http://localhost:9090"
}));

app.Run();

public partial class Program;
