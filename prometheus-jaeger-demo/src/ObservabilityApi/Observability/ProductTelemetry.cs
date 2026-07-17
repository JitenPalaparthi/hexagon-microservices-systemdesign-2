using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObservabilityApi.Observability;

public sealed class ProductTelemetry
{
    public const string ActivitySourceName = "ObservabilityApi.Products";
    public const string MeterName = "ObservabilityApi.Products";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    private readonly Counter<long> _productReads =
        Meter.CreateCounter<long>("products.read.count", description: "Number of product read operations.");

    private readonly Counter<long> _productsCreated =
        Meter.CreateCounter<long>("products.created.count", description: "Number of products created.");

    private readonly Counter<long> _productErrors =
        Meter.CreateCounter<long>("products.error.count", description: "Number of product operation errors.");

    private readonly Histogram<double> _operationDuration =
        Meter.CreateHistogram<double>("products.operation.duration", unit: "ms", description: "Product operation duration.");

    public void RecordRead(bool found, double elapsedMilliseconds)
    {
        _productReads.Add(1, new KeyValuePair<string, object?>("product.found", found));
        _operationDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("operation", "read"));
    }

    public void RecordCreated(double elapsedMilliseconds)
    {
        _productsCreated.Add(1);
        _operationDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("operation", "create"));
    }

    public void RecordError(string operation) =>
        _productErrors.Add(1, new KeyValuePair<string, object?>("operation", operation));
}
