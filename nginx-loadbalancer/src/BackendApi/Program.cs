using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var instanceName = Environment.GetEnvironmentVariable("INSTANCE_NAME") ?? Environment.MachineName;
var instanceColor = Environment.GetEnvironmentVariable("INSTANCE_COLOR") ?? "not-set";

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    instance = instanceName,
    timestampUtc = DateTimeOffset.UtcNow
}));

app.MapGet("/", (HttpContext context) => Results.Ok(CreateResponse(context, "root")));
app.MapGet("/api/info", (HttpContext context) => Results.Ok(CreateResponse(context, "info")));

app.MapGet("/api/work", async (HttpContext context, int delayMs = 100) =>
{
    delayMs = Math.Clamp(delayMs, 0, 5000);
    var stopwatch = Stopwatch.StartNew();
    await Task.Delay(delayMs, context.RequestAborted);
    stopwatch.Stop();

    return Results.Ok(new
    {
        message = "Work completed",
        instance = instanceName,
        color = instanceColor,
        requestedDelayMs = delayMs,
        elapsedMs = stopwatch.ElapsedMilliseconds,
        host = Environment.MachineName,
        processId = Environment.ProcessId,
        path = context.Request.Path.Value,
        forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString(),
        forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString(),
        requestId = context.TraceIdentifier,
        timestampUtc = DateTimeOffset.UtcNow
    });
});

app.MapPost("/api/echo", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync(context.RequestAborted);

    return Results.Ok(new
    {
        message = "Payload received",
        instance = instanceName,
        color = instanceColor,
        body,
        contentType = context.Request.ContentType,
        requestId = context.TraceIdentifier,
        timestampUtc = DateTimeOffset.UtcNow
    });
});

app.Run();

object CreateResponse(HttpContext context, string endpoint) => new
{
    message = "Response from .NET 10 backend",
    endpoint,
    instance = instanceName,
    color = instanceColor,
    host = Environment.MachineName,
    processId = Environment.ProcessId,
    method = context.Request.Method,
    path = context.Request.Path.Value,
    forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString(),
    forwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString(),
    requestId = context.TraceIdentifier,
    timestampUtc = DateTimeOffset.UtcNow
};
