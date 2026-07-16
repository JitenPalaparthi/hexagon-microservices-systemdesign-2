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
