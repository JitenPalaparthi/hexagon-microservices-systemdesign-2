#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE="${MINIKUBE_PROFILE:-minikube}"

command -v minikube >/dev/null 2>&1 || { echo "minikube is required" >&2; exit 1; }
command -v kubectl >/dev/null 2>&1 || { echo "kubectl is required" >&2; exit 1; }

if ! minikube status -p "$PROFILE" >/dev/null 2>&1; then
  minikube start -p "$PROFILE" --cpus=4 --memory=6144
fi

echo "Building application images inside Minikube..."
minikube image build -p "$PROFILE" -t products/product-grpc:1.0 -f "$ROOT/ProductGrpcServer/Dockerfile" "$ROOT"
minikube image build -p "$PROFILE" -t products/product-rest:1.0 -f "$ROOT/ProductRestServer/Dockerfile" "$ROOT"

echo "Applying Kubernetes resources..."
kubectl apply -k "$ROOT/k8s"

kubectl -n products-demo rollout status statefulset/postgres --timeout=180s
kubectl -n products-demo rollout status deployment/product-grpc --timeout=240s
kubectl -n products-demo rollout status deployment/product-rest --timeout=240s
kubectl -n products-demo rollout status deployment/nginx-gateway --timeout=180s
kubectl -n products-demo rollout status deployment/adminer --timeout=180s

echo
kubectl -n products-demo get pods,svc,pvc

echo
MINIKUBE_IP="$(minikube ip -p "$PROFILE")"
echo "REST:    http://${MINIKUBE_IP}:30080/api/products"
echo "gRPC:    ${MINIKUBE_IP}:30500 (plaintext h2c)"
echo "Adminer: http://${MINIKUBE_IP}:30081"
echo
echo "On macOS with the Docker driver, NodePort may not be directly reachable."
echo "Use: ./scripts/minikube-tunnel.sh"
