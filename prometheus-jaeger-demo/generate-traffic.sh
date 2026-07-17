#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"

for i in $(seq 1 20); do
  curl -fsS "$BASE_URL/api/products" >/dev/null
  curl -sS "$BASE_URL/api/products/$((RANDOM % 6 + 1))" >/dev/null
  curl -fsS "$BASE_URL/api/diagnostics/slow?milliseconds=$((RANDOM % 600 + 50))" >/dev/null

done

curl -fsS -X POST "$BASE_URL/api/products" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Observability Book","price":49.99,"stock":15}' >/dev/null

# Intentional HTTP 500 to produce an error trace.
curl -sS "$BASE_URL/api/diagnostics/error" >/dev/null || true

echo "Traffic generated. Open Prometheus at http://localhost:9090 and Jaeger at http://localhost:16686"
