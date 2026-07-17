#!/usr/bin/env bash
set -euo pipefail

cleanup() {
  jobs -p | xargs -r kill 2>/dev/null || true
}
trap cleanup EXIT INT TERM

kubectl -n products-demo port-forward service/nginx-gateway 8080:8080 5001:5000 &
kubectl -n products-demo port-forward service/adminer 8081:8080 &

echo "REST:    http://localhost:8080/api/products"
echo "gRPC:    localhost:5001 (plaintext h2c)"
echo "Adminer: http://localhost:8081"
echo "Press Ctrl+C to stop port forwarding."
wait
