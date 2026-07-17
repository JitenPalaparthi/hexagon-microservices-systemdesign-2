# .NET 10 REST API with Prometheus and Jaeger

## Architecture

- ASP.NET Core 10 REST API
- OpenTelemetry tracing exported over OTLP/gRPC to Jaeger
- OpenTelemetry metrics exposed at `/metrics`
- Prometheus scrapes the API every 5 seconds

## Start

```bash
docker compose up --build
```

## URLs

- API: http://localhost:8080
- Swagger-style endpoint list: http://localhost:8080
- Metrics: http://localhost:8080/metrics
- Prometheus: http://localhost:9090
- Jaeger: http://localhost:16686

## Test

```bash
curl http://localhost:8080/api/products
curl http://localhost:8080/api/products/1
curl -X POST http://localhost:8080/api/products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Mechanical Mouse","price":59.99,"stock":20}'
curl 'http://localhost:8080/api/diagnostics/slow?milliseconds=1000'
curl -i http://localhost:8080/api/diagnostics/error
./generate-traffic.sh
```

## Prometheus queries

```promql
up{job="dotnet10-observability-api"}
```

```promql
rate(http_server_request_duration_seconds_count[1m])
```

```promql
products_read_count_total
```

Metric names may be normalized by the exporter from dots to underscores.

## Jaeger

Open Jaeger, select `dotnet10-observability-api`, then click **Find Traces**.

## Clean rebuild

```bash
docker compose down -v
docker compose build --no-cache
docker compose up
```
