# .NET 10 REST API with Prometheus and Jaeger

This project demonstrates an ASP.NET Core .NET 10 REST service instrumented with OpenTelemetry.

## Architecture

```text
Client
  |
  | HTTP requests
  v
.NET 10 REST API :8080
  |                    \
  | /metrics (pull)     \ OTLP/gRPC traces (push)
  v                      v
Prometheus :9090       Jaeger :16686
```

## Included telemetry

- ASP.NET Core request metrics and traces
- .NET runtime and process metrics
- HTTP client instrumentation
- Custom counters and histograms for product operations
- Manual spans for repository-style operations
- Exception recording and intentional error endpoint
- `X-Trace-Id` response header for correlating a request with Jaeger

## Prerequisites

- Docker Desktop with Docker Compose
- `curl` for the sample requests

The host does not need a local .NET SDK because the Docker build uses the .NET 10 SDK image.

## Run

```bash
docker compose up --build
```

Services:

- REST API: http://localhost:8080
- Swagger-free API index: http://localhost:8080
- Prometheus: http://localhost:9090
- Jaeger UI: http://localhost:16686
- Raw metrics: http://localhost:8080/metrics
- Health check: http://localhost:8080/health

## Test the REST API

```bash
curl -i http://localhost:8080/api/products
curl -i http://localhost:8080/api/products/1
curl -i http://localhost:8080/api/products/999

curl -i -X POST http://localhost:8080/api/products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Mechanical Mouse","price":59.99,"stock":20}'

curl -i 'http://localhost:8080/api/diagnostics/slow?milliseconds=1200'
curl -i http://localhost:8080/api/diagnostics/error
```

Generate multiple requests:

```bash
./generate-traffic.sh
```

## Prometheus queries

Open http://localhost:9090 and try:

```promql
up{job="dotnet10-observability-api"}
```

```promql
rate(http_server_request_duration_seconds_count[1m])
```

```promql
histogram_quantile(
  0.95,
  sum by (le) (rate(http_server_request_duration_seconds_bucket[5m]))
)
```

```promql
products_read_count_total
```

```promql
rate(products_created_count_total[5m])
```

Metric names can be normalized by the Prometheus exporter. Browse the API's `/metrics` endpoint to see the exact emitted names.

## View traces in Jaeger

1. Open http://localhost:16686.
2. Select `dotnet10-observability-api` in the **Service** dropdown.
3. Click **Find Traces**.
4. Run the slow and error endpoints to see latency and exception details.
5. Copy the `X-Trace-Id` response header and search for that trace ID in Jaeger.

## Stop and clean up

```bash
docker compose down
```

Remove the Prometheus data volume too:

```bash
docker compose down -v
```

## Project layout

```text
.
├── docker-compose.yml
├── generate-traffic.sh
├── prometheus/
│   └── prometheus.yml
└── src/ObservabilityApi/
    ├── Controllers/
    ├── Models/
    ├── Observability/
    ├── Dockerfile
    ├── ObservabilityApi.csproj
    ├── Program.cs
    └── appsettings.json
```
