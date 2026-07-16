# Azure Service Bus Point-to-Point — .NET 10 + Local Docker Emulator

This project demonstrates **point-to-point messaging** with:

- Azure Service Bus local emulator
- One `orders` queue
- .NET 10 console producer
- .NET 10 console consumer
- Docker Compose
- `PeekLock` receive mode with explicit completion
- Dead-lettering after repeated failures

## Architecture

```text
+----------------------+       send        +------------------------+
| OrderProducer        | ----------------> | Azure Service Bus      |
| .NET 10 console app  |                   | queue: orders          |
+----------------------+                   +-----------+------------+
                                                       |
                                                       | one message is delivered
                                                       | to one competing consumer
                                                       v
                                           +------------------------+
                                           | OrderConsumer          |
                                           | .NET 10 console app    |
                                           +------------------------+
```

A queue implements point-to-point messaging. Even when several consumer instances run, a successfully completed message is processed by only one consumer.

## Prerequisites

- Docker Desktop with Docker Compose
- At least 2 GB free RAM and 5 GB free disk space
- No Azure subscription is required

> The emulator is for local development and testing only, not production.

## Run everything

From this directory:

```bash
docker compose up --build
```

You should see producer output similar to:

```text
SENT seq=1 id=... product=Monitor qty=2
```

And consumer output similar to:

```text
RECEIVED by=consumer-1 seq=1 id=... product=Monitor qty=2 deliveryCount=1
COMPLETED seq=1
```

Stop all containers:

```bash
docker compose down
```

Remove containers and persisted emulator data:

```bash
docker compose down -v
```

## Useful commands

Show running containers:

```bash
docker compose ps
```

Follow producer logs:

```bash
docker compose logs -f order-producer
```

Follow consumer logs:

```bash
docker compose logs -f order-consumer
```

Check emulator health:

```bash
curl http://localhost:5300/health
```

## Demonstrate competing consumers

Stop the default stack and start three instances of the same consumer:

```bash
docker compose down
docker compose up --build --scale order-consumer=3
```

Docker Compose distributes messages across the three consumer containers. A single queue message is not broadcast to all three. This remains point-to-point communication.

Because `container_name` is not set on `order-consumer`, scaling is supported. Each container uses its hostname internally, although the configured display name remains `consumer-1` unless you remove `CONSUMER_NAME` from `docker-compose.yml`.

## Run .NET applications outside Docker

Start only the emulator and SQL Server:

```bash
docker compose up -d mssql servicebus-emulator
```

For applications running directly on your host, use:

```bash
export SERVICEBUS_CONNECTION_STRING='Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
export SERVICEBUS_QUEUE='orders'
```

Run the consumer:

```bash
dotnet run --project src/OrderConsumer
```

Run the producer in another terminal:

```bash
dotnet run --project src/OrderProducer
```

## Message settlement

The consumer uses `PeekLock`:

1. Service Bus locks the message for the receiver.
2. The consumer performs its business operation.
3. `CompleteMessageAsync` permanently removes the message after success.
4. On processing failure, `AbandonMessageAsync` makes it available for redelivery.
5. After `MaxDeliveryCount` is reached, Service Bus moves it to the dead-letter queue.

This provides **at-least-once delivery**, so production consumers should make business processing idempotent.

## Key files

```text
.
├── docker-compose.yml
├── emulator/Config.json
└── src
    ├── OrderProducer
    │   ├── Program.cs
    │   ├── OrderProducer.csproj
    │   └── Dockerfile
    └── OrderConsumer
        ├── Program.cs
        ├── OrderConsumer.csproj
        └── Dockerfile
```

## macOS Apple Silicon note

The Compose file specifies `platform: linux/amd64` for SQL Server and the emulator. Docker Desktop can run them through architecture emulation. Startup may therefore be slower on Apple Silicon.
