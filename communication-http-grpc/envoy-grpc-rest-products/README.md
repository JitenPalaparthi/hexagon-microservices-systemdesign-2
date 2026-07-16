# Envoy gRPC → REST/JSON Transcoding Demo

This project deploys a **.NET 10 native gRPC service** behind **Envoy**. Clients can use ordinary HTTP/JSON with `curl`; Envoy converts each request to a native HTTP/2 gRPC call.

```text
curl / Postman
     |
     | HTTP/1.1 or HTTP/2 + JSON
     v
Envoy :8080
  grpc_json_transcoder
     |
     | HTTP/2 + Protobuf (native gRPC)
     v
ProductGrpcServer :5001
```

The backend does **not** implement REST controllers. REST paths come from `google.api.http` annotations in `products.proto`.

## Requirements

- Docker Desktop
- Docker Compose
- `curl`

`grpcurl` is optional because the README also shows how to run it in a container.

## Start

From the project directory:

```bash
docker compose up --build
```

Wait until Envoy reports that it has started. Public endpoints:

- REST/JSON through Envoy: `http://localhost:8080`
- Envoy admin interface: `http://localhost:9901`
- The gRPC backend is intentionally not published to the host.

## REST calls through Envoy

### List products

```bash
curl -s http://localhost:8080/api/products | jq
```

### Get one product

```bash
curl -s http://localhost:8080/api/products/1 | jq
```

### Create

```bash
curl -s -X POST http://localhost:8080/api/products \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "4K Monitor",
    "description": "Created through Envoy",
    "price": 32999
  }' | jq
```

### Update

The URL value `{id}` is copied by Envoy into the protobuf `id` field.

```bash
curl -s -X PUT http://localhost:8080/api/products/1 \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "Mechanical Keyboard Pro",
    "description": "Updated through Envoy",
    "price": 8999
  }' | jq
```

### Delete

```bash
curl -s -X DELETE http://localhost:8080/api/products/2 | jq
```

### Run all tests

```bash
./scripts/test-rest.sh
```

## Call native gRPC directly inside the Docker network

Reflection is enabled in the .NET service. Run `grpcurl` as a temporary Compose service-network container:

```bash
docker run --rm \
  --network envoy-grpc-rest-products_default \
  fullstorydev/grpcurl:latest \
  -plaintext product-grpc:5001 list
```

List products:

```bash
docker run --rm \
  --network envoy-grpc-rest-products_default \
  fullstorydev/grpcurl:latest \
  -plaintext \
  -d '{}' \
  product-grpc:5001 \
  products.v1.ProductService/ListProducts
```

Get product 1:

```bash
docker run --rm \
  --network envoy-grpc-rest-products_default \
  fullstorydev/grpcurl:latest \
  -plaintext \
  -d '{"id":1}' \
  product-grpc:5001 \
  products.v1.ProductService/GetProduct
```

If Compose creates a differently named network, find it with:

```bash
docker network ls
```

## Important files

```text
ProductGrpcServer/
  Protos/products.proto          gRPC contract and REST mappings
  Services/ProductsService.cs   gRPC implementation
  Services/ProductRepository.cs in-memory data
  Program.cs                    HTTP/2-only Kestrel gRPC endpoint
  Dockerfile

envoy/
  envoy.yaml                    listener, transcoder and HTTP/2 upstream
  product.pb                    compiled protobuf descriptor set

docker-compose.yml
scripts/test-rest.sh
```

## How Envoy performs the conversion

For this HTTP request:

```http
GET /api/products/1
```

Envoy reads the mapping:

```proto
rpc GetProduct(GetProductRequest) returns (ProductReply) {
  option (google.api.http) = {
    get: "/api/products/{id}"
  };
}
```

It creates the protobuf request:

```json
{"id": 1}
```

and invokes the native gRPC method:

```text
/products.v1.ProductService/GetProduct
```

The gRPC protobuf response is converted back to JSON before Envoy returns it to `curl`.

## Descriptor regeneration

`envoy/product.pb` is already included. Regenerate it after changing the `.proto` file using a local `protoc` installation:

```bash
protoc \
  -I ProductGrpcServer/Protos \
  -I /path/to/protobuf/includes \
  --include_imports \
  --descriptor_set_out=envoy/product.pb \
  ProductGrpcServer/Protos/products.proto
```

The descriptor must include imported files because the REST annotations depend on `google/api/annotations.proto` and `google/api/http.proto`.

## Stop

```bash
docker compose down
```

Reset the in-memory products by restarting the containers.
