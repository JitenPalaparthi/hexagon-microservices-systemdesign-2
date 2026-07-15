# .NET 10 Interservice Communication with Azure Service Bus

This runnable solution demonstrates three forms of microservice communication:

1. **Synchronous HTTP** — `OrderService` calls `ProductService` and waits for a response.
2. **Asynchronous messaging** — `CheckoutService` publishes an `OrderCreatedEvent` to an Azure Service Bus topic and returns `202 Accepted`.
3. **Event-driven architecture** — `InventoryWorker` and `NotificationWorker` receive independent copies through separate subscriptions.
4. **Calling a service from an event handler** — `InventoryWorker` calls `WarehouseService` over HTTP.
5. **Idempotency** — `WarehouseService` uses the `Idempotency-Key` header to avoid duplicate allocations during message redelivery.
6. **Peek-lock processing** — workers explicitly complete, abandon, or dead-letter messages.

## Architecture

```text
Synchronous:
Client -> OrderService -> ProductService -> response

Asynchronous/event-driven:
Client -> CheckoutService -> Azure Service Bus orders-topic
                              |-> inventory-subscription -> InventoryWorker -> WarehouseService
                              |-> notification-subscription -> NotificationWorker
```

## Projects

```text
Contracts/             Shared integration-event contracts
ProductService/        Product query API
OrderService/          Synchronous HTTP caller
CheckoutService/       Azure Service Bus topic publisher
InventoryWorker/       Topic subscription consumer and Warehouse HTTP caller
NotificationWorker/    Independent topic subscription consumer
WarehouseService/      Idempotent allocation API
servicebus/Config.json Emulator topic and subscription configuration
```

## Prerequisites

- Docker Desktop with Docker Compose
- At least 5 GB free disk space and 2 GB available memory for the emulator
- Optional: .NET 10 SDK for running projects outside Docker

The Compose setup uses the Microsoft Azure Service Bus emulator and its SQL dependency. The emulator is for development and testing only.

## Run everything

```bash
docker compose up --build
```

The emulator may require some startup time on the first run. Application containers use `restart: on-failure` so they reconnect after it becomes available.

Check the emulator health endpoint:

```bash
curl http://localhost:5300/health
```

## Test synchronous communication

```bash
curl -X POST http://localhost:5002/orders/synchronous \
  -H 'Content-Type: application/json' \
  -d '{"productId":101,"quantity":2}'
```

Flow:

```text
Client -> OrderService -> HTTP GET ProductService -> OrderService -> Client
```

## Test asynchronous event-driven communication

```bash
curl -X POST http://localhost:5003/orders/asynchronous \
  -H 'Content-Type: application/json' \
  -d '{
    "customerId":"11111111-1111-1111-1111-111111111111",
    "items":[
      {"productId":101,"quantity":2,"unitPrice":85000},
      {"productId":103,"quantity":1,"unitPrice":1800}
    ]
  }'
```

Expected HTTP behavior:

```http
HTTP/1.1 202 Accepted
```

Both subscriptions receive the event independently:

```bash
docker compose logs -f checkout-service inventory-worker notification-worker warehouse-service
```

## Azure Service Bus entities

The emulator reads `servicebus/Config.json` at startup and creates:

```text
Topic: orders-topic
  Subscription: inventory-subscription
  Subscription: notification-subscription
```

Each subscription has:

- One-minute message lock
- Maximum delivery count of five
- Dead-lettering when an expired message is encountered
- One-hour default message TTL

## Message settlement behavior

`InventoryWorker` and `NotificationWorker` use explicit settlement:

```text
Successful processing       -> Complete
Transient processing error  -> Abandon, allowing redelivery
Invalid JSON                 -> Dead-letter
Max delivery count exceeded -> Service Bus moves the message to the DLQ
```

## Use a real Azure Service Bus namespace

Replace the emulator connection string with your Azure namespace connection string:

```bash
export ConnectionStrings__ServiceBus='Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<policy>;SharedAccessKey=<key>'
```

For production, prefer Microsoft Entra ID with managed identity rather than storing shared-access keys.

Create the same entities in Azure:

```text
orders-topic
inventory-subscription
notification-subscription
```

The application code does not need to change.

## Stop and clean up

```bash
docker compose down
```

Remove emulator data and rebuilt resources:

```bash
docker compose down --volumes --remove-orphans
```

## Production improvements

- Use managed identity and role-based access control.
- Add a transactional outbox between the order database and event publisher.
- Store consumer idempotency records in a durable database.
- Apply exponential-backoff retries with jitter for downstream HTTP calls.
- Monitor and replay dead-letter messages through an operational workflow.
- Add OpenTelemetry trace propagation across HTTP and Service Bus messages.
- Version integration-event schemas and test backward compatibility.
- Configure topic subscription filters when consumers need only selected event types.

## Worker project build requirement

Both worker projects explicitly reference `Microsoft.Extensions.Hosting` because the Worker SDK does not supply the hosting assemblies by itself:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
```

The worker runtime images use `mcr.microsoft.com/dotnet/runtime:10.0`.

## Build fix included

`InventoryWorker` uses `IHttpClientFactory` and `AddHttpClient`, so its project explicitly references `Microsoft.Extensions.Http` 10.0.0.
