#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${BASE_URL:-http://localhost:8080}"

echo '1. List products'
curl -sS "$BASE_URL/api/products"; echo

echo '2. Get product 1'
curl -sS "$BASE_URL/api/products/1"; echo

echo '3. Create product'
curl -sS -X POST "$BASE_URL/api/products" \
  -H 'Content-Type: application/json' \
  -d '{"name":"4K Monitor","description":"Created through Envoy REST transcoding","price":32999}'; echo

echo '4. Update product 1'
curl -sS -X PUT "$BASE_URL/api/products/1" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Mechanical Keyboard Pro","description":"Updated through Envoy","price":8999}'; echo

echo '5. Delete product 2'
curl -sS -X DELETE "$BASE_URL/api/products/2"; echo
