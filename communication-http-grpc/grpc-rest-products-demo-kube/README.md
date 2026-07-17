# .NET 10 Products: REST + gRPC + NGINX Load Balancer

This project exposes the same PostgreSQL-backed product data through two independent .NET 10 services:

- **REST API** over HTTP/1.1
- **gRPC API** over plaintext HTTP/2 (`h2c`)
- **NGINX** as the public gateway and load balancer
- Two REST instances and two gRPC instances
- PostgreSQL 17 and Adminer

## Architecture

```text
                         NGINX Gateway
              ┌────────────────────────────┐
REST client ──►│ localhost:8080 /api/*      │
              │       least_conn           │──► REST instance 1 ─┐
              │                            │──► REST instance 2 ─┤
              │                            │                     │
gRPC client ─►│ localhost:5000 HTTP/2 h2c  │                     ├──► PostgreSQL
              │       least_conn           │──► gRPC instance 1 ┤
              │                            │──► gRPC instance 2 ─┘
              └────────────────────────────┘
```

NGINX uses separate public ports because local plaintext REST uses HTTP/1.1 while plaintext gRPC requires HTTP/2. In production, both can share one TLS port using ALPN and path/service routing.

## Start

```bash
docker compose down -v --remove-orphans
docker compose up --build -d
docker compose ps
```

Logs:

```bash
docker compose logs -f nginx product-rest-1 product-rest-2 product-grpc-1 product-grpc-2
```

## Public endpoints

| Protocol | Public endpoint | NGINX target |
|---|---|---|
| REST | `http://localhost:8080/api/products` | `product-rest-1:8080`, `product-rest-2:8080` |
| gRPC | `localhost:5000` | `product-grpc-1:8080`, `product-grpc-2:8080` |
| Adminer | `http://localhost:8081` | PostgreSQL administration |

## REST tests

List products:

```bash
curl -i http://localhost:8080/api/products
```

The response includes `X-Backend-Instance: rest-1` or `rest-2`. Repeat the request to observe load balancing.

Get one product:

```bash
curl -i http://localhost:8080/api/products/1
```

Filter by category:

```bash
curl -i 'http://localhost:8080/api/products?category=Accessories'
```

Create a product:

```bash
curl -i -X POST http://localhost:8080/api/products \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "Webcam",
    "description": "4K USB webcam",
    "category": "Accessories",
    "price": 12999,
    "availableQuantity": 12
  }'
```

Update price:

```bash
curl -i -X PATCH http://localhost:8080/api/products/1/price \
  -H 'Content-Type: application/json' \
  -d '{
    "newPrice": 189999,
    "reason": "Festival discount"
  }'
```

Delete:

```bash
curl -i -X DELETE http://localhost:8080/api/products/6
```

## gRPC tests with Postman

1. Create a new gRPC request.
2. Server URL: `localhost:5001`.
3. Disable TLS; this endpoint uses h2c.
4. Import `ProductGrpcServer/Protos/products.proto` if reflection is not automatically discovered through NGINX.
5. Select a method such as `products.v1.ProductService/ListProducts`.

For `GetProduct`:

```json
{
  "id": 1
}
```

For `CreateProduct`:

```json
{
  "name": "External SSD",
  "description": "1 TB USB-C SSD",
  "category": "Storage",
  "price": 10999,
  "availableQuantity": 18
}
```

All existing unary and streaming methods remain available:

- `GetProduct`
- `CreateProduct`
- `ListProducts`
- `StreamProducts`
- `CreateProducts`
- `UpdateProductPrices`

## Verify shared data

Create or update a product through REST, then call gRPC `ListProducts`. The change appears because both services use the same `products` table. The reverse direction works as well.

## Adminer

Open `http://localhost:8081`:

```text
System:   PostgreSQL
Server:   postgres
Username: appuser
Password: apppassword
Database: productsdb
```

## Important NGINX routing

```nginx
# HTTP/1.1 REST endpoint
listen 8080;
location /api/ {
    proxy_pass http://rest_products;
}

# HTTP/2 gRPC endpoint
listen 5000 http2;
location / {
    grpc_pass grpc://grpc_products;
}
```

---

# Run on Kubernetes with Minikube

The `k8s/` directory deploys the complete application to the `products-demo` namespace:

- PostgreSQL 17 as a `StatefulSet` with a 1 GiB persistent volume
- Two REST API pods behind a Kubernetes `Service`
- Two gRPC pods behind a Kubernetes `Service`
- NGINX gateway with separate REST and plaintext HTTP/2 gRPC ports
- Adminer exposed for database inspection
- Kubernetes Secrets, ConfigMap, readiness probes, liveness probes, and resource limits

## Prerequisites

```bash
minikube version
kubectl version --client
```

Docker Desktop should be running when Minikube uses the Docker driver.

## Automated deployment

From the project root:

```bash
chmod +x scripts/*.sh
./scripts/minikube-deploy.sh
```

The script:

1. Starts Minikube with 4 CPUs and 6 GiB memory if it is not already running.
2. Builds both .NET 10 images directly into Minikube.
3. Applies all Kubernetes resources using Kustomize.
4. Waits for PostgreSQL, REST, gRPC, NGINX, and Adminer to become ready.

## Check Kubernetes resources

```bash
kubectl get all -n products-demo
kubectl get pvc -n products-demo
kubectl get pods -n products-demo -o wide
```

Watch application logs:

```bash
kubectl logs -n products-demo -l app=product-rest --all-containers=true --tail=100 -f
kubectl logs -n products-demo -l app=product-grpc --all-containers=true --tail=100 -f
kubectl logs -n products-demo deployment/nginx-gateway --tail=100 -f
```

## Access on macOS

With the Minikube Docker driver, the most reliable access method is port forwarding:

```bash
./scripts/minikube-tunnel.sh
```

Keep that terminal open. The endpoints become:

| Component | Endpoint |
|---|---|
| REST API | `http://localhost:8080/api/products` |
| gRPC API | `localhost:5001` using plaintext HTTP/2 (`h2c`) |
| Adminer | `http://localhost:8081` |

Test REST:

```bash
curl -i http://localhost:8080/api/products
```

Repeat the call to see different `X-Backend-Instance` response headers from the two REST pods:

```bash
for i in {1..6}; do
  curl -sI http://localhost:8080/api/products | grep -i x-backend-instance
done
```

Test gRPC using Postman:

1. Create a gRPC request.
2. Set the server URL to `localhost:5001`.
3. Disable TLS.
4. Import `ProductGrpcServer/Protos/products.proto` when needed.
5. Call `products.v1.ProductService/ListProducts`.

Test using `grpcurl`:

```bash
grpcurl -plaintext localhost:5001 list
grpcurl -plaintext localhost:5001 list products.v1.ProductService
grpcurl -plaintext -d '{}' localhost:5001 products.v1.ProductService/ListProducts
```

Adminer connection details:

```text
System:   PostgreSQL
Server:   postgres
Username: appuser
Password: apppassword
Database: productsdb
```

## Direct NodePort access

On environments where the Minikube IP is reachable:

```bash
MINIKUBE_IP=$(minikube ip)
curl "http://${MINIKUBE_IP}:30080/api/products"
```

NodePorts:

- REST gateway: `30080`
- gRPC gateway: `30500`
- Adminer: `30081`

## Scale services

```bash
kubectl scale deployment/product-rest -n products-demo --replicas=4
kubectl scale deployment/product-grpc -n products-demo --replicas=4
kubectl get pods -n products-demo -w
```

Restore two replicas:

```bash
kubectl scale deployment/product-rest -n products-demo --replicas=2
kubectl scale deployment/product-grpc -n products-demo --replicas=2
```

## Restart a deployment

```bash
kubectl rollout restart deployment/product-rest -n products-demo
kubectl rollout status deployment/product-rest -n products-demo
```

## Delete the application

```bash
./scripts/minikube-delete.sh
```

This removes the namespace and its persistent database volume. To remove the complete Minikube cluster:

```bash
minikube delete
```
