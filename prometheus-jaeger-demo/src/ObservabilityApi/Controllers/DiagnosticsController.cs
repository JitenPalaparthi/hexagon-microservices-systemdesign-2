using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ObservabilityApi.Observability;

namespace ObservabilityApi.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(ProductTelemetry telemetry) : ControllerBase
{
    [HttpGet("slow")]
    public async Task<IActionResult> Slow([FromQuery] int milliseconds = 750, CancellationToken cancellationToken = default)
    {
        milliseconds = Math.Clamp(milliseconds, 0, 10_000);
        using var activity = ProductTelemetry.ActivitySource.StartActivity("diagnostics.slow", ActivityKind.Internal);
        activity?.SetTag("delay.milliseconds", milliseconds);
        await Task.Delay(milliseconds, cancellationToken);
        return Ok(new { delayedForMilliseconds = milliseconds, traceId = Activity.Current?.TraceId.ToString() });
    }

    [HttpGet("error")]
    public IActionResult Error()
    {
        using var activity = ProductTelemetry.ActivitySource.StartActivity("diagnostics.error", ActivityKind.Internal);
        telemetry.RecordError("diagnostics.error");
        activity?.SetStatus(ActivityStatusCode.Error, "Intentional demo error");
        activity?.AddEvent(new ActivityEvent("intentional.error.triggered"));
        throw new InvalidOperationException("Intentional exception generated for Jaeger tracing demonstration.");
    }
}
