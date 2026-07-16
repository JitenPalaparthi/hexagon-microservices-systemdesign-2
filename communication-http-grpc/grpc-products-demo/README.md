# .NET 10 gRPC Products Server

A server-only gRPC example using .NET 10, PostgreSQL 17, Entity Framework Core, Adminer, Docker Compose, and Postman. It demonstrates every gRPC communication pattern in one service:

| Method | Pattern | Purpose |
|---|---|---|
| `GetProduct` | Unary | Fetch one product |
| `CreateProduct` | Unary | Insert one product |
| `StreamProducts` | Server streaming | Send products one by one |
| `CreateProducts` | Client streaming | Receive and insert multiple products |
| `UpdateProductPrices` | Bidirectional streaming | Send price updates and receive one reply for each update |
| `ListProducts` | Unary | Return all products in one response |

## Architecture

```text
Postman gRPC client
        │ HTTP/2 (h2c), port 5001
        ▼
.NET 10 Product gRPC Server
        │ EF Core / Npgsql
        ▼
PostgreSQL 17  ◄──── Adminer on port 8081
```

The application has two server ports:

- `localhost:5001`: plaintext HTTP/2 for gRPC.
- `http://localhost:5002`: HTTP/1.1 health and information endpoints.

## Why `Grpc.Tools` is version 2.68.1

The runtime packages use gRPC 2.80.0, but the build-time `Grpc.Tools` package is pinned to 2.68.1. Versions beginning at 2.69.0 have a known `linux_arm64/protoc exited with code 139` regression in Docker builds on Apple Silicon. The project uses `Grpc.AspNetCore.Server`, not the `Grpc.AspNetCore` metapackage, so NuGet does not force `Grpc.Tools` 2.80.0.

## Start

```bash
docker compose down -v --remove-orphans
docker compose build --no-cache
docker compose up -d
```

Check containers:

```bash
docker compose ps
```

Check server logs:

```bash
docker compose logs -f product-grpc-server
```

Health check:

```bash
curl http://localhost:5002/health
```

Expected:

```json
{"status":"ok","database":"postgresql"}
```

## Adminer

Open `http://localhost:8081` and use:

```text
System:   PostgreSQL
Server:   postgres
Username: appuser
Password: apppassword
Database: productsdb
```

Select the `products` table to inspect seeded and newly inserted records.

# Test with Postman

Postman desktop supports unary, server-streaming, client-streaming, and bidirectional-streaming gRPC calls.

## Create the request

1. Open Postman.
2. Select **New → gRPC**.
3. Enter the server URL: `localhost:5001`.
4. Keep TLS disabled because this local endpoint uses plaintext HTTP/2 (`h2c`).
5. Click the service-definition selector.
6. Use server reflection to load `products.v1.ProductService`.

If reflection is not discovered automatically, import `ProductGrpcServer/Protos/products.proto` into Postman. The proto imports standard Google protobuf types; Postman normally resolves these built-in definitions.

## 1. Unary: GetProduct

Select:

```text
products.v1.ProductService/GetProduct
```

Message:

```json
{
  "id": 1
}
```

Click **Invoke**. One `ProductReply` is returned.

## 2. Unary: CreateProduct

Select:

```text
products.v1.ProductService/CreateProduct
```

Message:

```json
{
  "name": "Webcam",
  "description": "4K USB webcam",
  "category": "Accessories",
  "price": 12999,
  "availableQuantity": 12
}
```

Click **Invoke**. The database-generated ID is returned.

## 3. Server streaming: StreamProducts

Select:

```text
products.v1.ProductService/StreamProducts
```

Message:

```json
{
  "category": "",
  "delayMilliseconds": 1000
}
```

Click **Invoke**. The server sends one `ProductReply` every second until all matching products have been sent.

Filter by category:

```json
{
  "category": "Accessories",
  "delayMilliseconds": 500
}
```

This method demonstrates:

```text
One client request ──► Product #1
                    ◄─ Product #2
                    ◄─ Product #3
                    ◄─ ...
```

## 4. Client streaming: CreateProducts

Select:

```text
products.v1.ProductService/CreateProducts
```

Start the call. Use Postman's message editor to send these messages one at a time.

Message 1:

```json
{
  "name": "External SSD",
  "description": "1 TB USB-C SSD",
  "category": "Storage",
  "price": 10999,
  "availableQuantity": 18
}
```

Message 2:

```json
{
  "name": "Noise-Cancelling Headphones",
  "description": "Wireless over-ear headphones",
  "category": "Audio",
  "price": 15999,
  "availableQuantity": 8
}
```

Message 3:

```json
{
  "name": "Laptop Stand",
  "description": "Aluminium adjustable stand",
  "category": "Accessories",
  "price": 3499,
  "availableQuantity": 30
}
```

After sending all messages, click **End streaming**. The server then returns one summary:

```json
{
  "receivedCount": 3,
  "createdCount": 3,
  "failedCount": 0,
  "createdProductIds": [6, 7, 8],
  "errors": []
}
```

This method demonstrates:

```text
Product #1 ──►
Product #2 ──► Server ──► one final summary
Product #3 ──►
```

## 5. Bidirectional streaming: UpdateProductPrices

Select:

```text
products.v1.ProductService/UpdateProductPrices
```

This keeps one HTTP/2 call open in both directions. Send each request separately in Postman.

Message 1:

```json
{
  "productId": 1,
  "newPrice": 189999,
  "reason": "Festival discount",
  "correlationId": "price-update-001"
}
```

Expected reply:

```json
{
  "productId": 1,
  "productName": "MacBook Pro",
  "previousPrice": 199999,
  "newPrice": 189999,
  "accepted": true,
  "message": "Product price updated successfully: Festival discount",
  "correlationId": "price-update-001"
}
```

Message 2:

```json
{
  "productId": 2,
  "newPrice": 6999,
  "reason": "Promotional price",
  "correlationId": "price-update-002"
}
```

Invalid example:

```json
{
  "productId": 2,
  "newPrice": -10,
  "reason": "Invalid test",
  "correlationId": "price-update-invalid"
}
```

The server replies with `accepted: false` but keeps the bidirectional stream open, so more updates can still be sent.

After sending all messages, click **End streaming** in Postman. Verify the persisted prices with `ListProducts` or Adminer.

## 6. Unary: ListProducts

Select:

```text
products.v1.ProductService/ListProducts
```

The request is `google.protobuf.Empty`, so use:

```json
{}
```

Click **Invoke** to return all products in one response.

# Useful commands

Rebuild only the server:

```bash
docker compose build --no-cache product-grpc-server
```

Restart only the server:

```bash
docker compose up -d --force-recreate product-grpc-server
```

Follow logs:

```bash
docker compose logs -f product-grpc-server postgres
```

Reset the database and seed data:

```bash
docker compose down -v
docker compose up -d --build
```

Inspect package restoration during troubleshooting:

```bash
docker compose build --no-cache --progress=plain product-grpc-server
```

The output should reference:

```text
/root/.nuget/packages/grpc.tools/2.68.1/
```

# Project structure

```text
grpc-products-dotnet10-complete/
├── docker-compose.yml
├── grpc-products-dotnet10.slnx
├── README.md
└── ProductGrpcServer/
    ├── ProductGrpcServer.csproj
    ├── Dockerfile
    ├── Program.cs
    ├── appsettings.json
    ├── Protos/
    │   └── products.proto
    ├── Entities/
    │   └── ProductEntity.cs
    ├── Data/
    │   ├── ProductDbContext.cs
    │   └── DatabaseInitializer.cs
    └── Services/
        └── ProductGrpcService.cs
```

# Important implementation details

- `ProductEntity` is the only EF Core database entity.
- Generated protobuf classes are in `ProductGrpcServer.Grpc`; they never collide with the EF entity.
- The database initializer receives both `ProductDbContext` and `ILogger`.
- `Category` exists consistently in the proto, entity, seed data, and service mappings.
- Reflection is enabled so Postman can discover the service.
- Client-stream validation failures are returned in the final summary instead of terminating the whole stream.
- Duplex inventory updates reject changes that would make inventory negative.
