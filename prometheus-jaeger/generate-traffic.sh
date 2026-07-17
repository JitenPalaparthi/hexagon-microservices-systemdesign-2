#!/usr/bin/env sh
set -eu
BASE_URL="${BASE_URL:-http://localhost:8080}"

curl -fsS "$BASE_URL/api/products" >/dev/null
curl -fsS "$BASE_URL/api/products/1" >/dev/null
curl -fsS -X POST "$BASE_URL/api/products" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Observability Mouse","price":59.99,"stock":20}' >/dev/null
curl -fsS "$BASE_URL/api/diagnostics/slow?milliseconds=750" >/dev/null
curl -sS "$BASE_URL/api/diagnostics/error" >/dev/null || true

echo "Traffic generated."
