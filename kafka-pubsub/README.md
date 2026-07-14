# Corrected Kafka Pub/Sub Project

The Kafka initializer was corrected to avoid Docker Compose variable interpolation. The earlier `${attempt}` and `$READY` shell expressions were expanded by Compose before Bash executed, causing topic initialization to fail. The initializer now uses a variable-free `until` loop.

For exact execution and verification commands, see [`STEP-BY-STEP.md`](STEP-BY-STEP.md).

# .NET 10 REST + Kafka Pub/Sub Example

A ready-to-run event-driven sample with:

- **OrderApi**: ASP.NET Core .NET 10 REST service.
- **PostgreSQL**: stores orders and transactional outbox messages.
- **Apache Kafka 4.1.2**: KRaft mode, no ZooKeeper.
- **OrderConsumer**: .NET 10 Worker Service that consumes `OrderCreatedEvent` messages.
- **Idempotent consumer**: duplicate Kafka deliveries do not create duplicate database records.

## Architecture

```text
Client
  |
  | POST /api/orders
  v
OrderApi (.NET 10)
  |  one PostgreSQL transaction
  +----> orders
  +----> outbox_messages
              |
              | background outbox publisher
              v
       Kafka: orders.created.v1
              |
              v
OrderConsumer (.NET 10 Worker)
              |
              +----> consumerdb.processed_orders
                     unique(event_id)
```

## Why the outbox is included

Publishing directly after inserting an order creates a dual-write failure window: the database commit may succeed while Kafka publication fails. This sample writes both the order and its event to PostgreSQL in one transaction. A background publisher retries unpublished outbox records until Kafka acknowledges them.

Kafka delivery is treated as **at least once**. The consumer commits the Kafka offset only after its database operation succeeds and uses `event_id` as a primary key to safely ignore duplicates.

## Prerequisites

- Docker Desktop or Docker Engine with Docker Compose v2.
- `curl` for the commands below.
- The .NET SDK is not required when running everything through Docker.

## Start everything

```bash
docker compose up --build -d
```

Check status:

```bash
docker compose ps
```

Follow application logs:

```bash
docker compose logs -f order-api order-consumer
```

The REST API is available at `http://localhost:8080`.

## Create an order

```bash
curl -i -X POST http://localhost:8080/api/orders \
  -H 'Content-Type: application/json' \
  -d '{
    "customerName": "Jiten",
    "product": "Mechanical Keyboard",
    "quantity": 2
  }'
```

Expected response shape:

```json
{
  "id": "4aa07ec1-33db-48c8-a30f-f18e14d52cd1",
  "customerName": "Jiten",
  "product": "Mechanical Keyboard",
  "quantity": 2,
  "createdAtUtc": "2026-07-13T08:30:00+00:00",
  "eventStatus": "PendingPublication"
}
```

The response initially says `PendingPublication` because the outbox publisher runs asynchronously. Query it again using the returned ID:

```bash
curl http://localhost:8080/api/orders/ORDER_ID
```

After publication, `eventStatus` becomes `Published`.

## Verify the consumer

```bash
docker compose exec postgres \
  psql -U appuser -d consumerdb \
  -c 'TABLE processed_orders;'
```

Verify source records and outbox state:

```bash
docker compose exec postgres psql -U appuser -d ordersdb -c 'TABLE orders;'
docker compose exec postgres psql -U appuser -d ordersdb -c 'TABLE outbox_messages;'
```

Inspect Kafka topic metadata:

```bash
docker compose exec kafka \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --describe --topic orders.created.v1
```

Read events manually:

```bash
docker compose exec kafka \
  /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server kafka:9092 \
  --topic orders.created.v1 \
  --from-beginning
```

## OpenAPI document

```bash
```

## Run services locally and infrastructure in Docker

Start only PostgreSQL, Kafka, and topic initialization:

```bash
docker compose up -d postgres kafka kafka-init
```

Then, with the .NET 10 SDK installed:

```bash
dotnet run --project src/OrderApi
dotnet run --project src/OrderConsumer
```

Local application settings use PostgreSQL on `localhost:5432` and Kafka on `localhost:29092`.

## Important implementation details

### Producer guarantees

- `Acks=All` requests acknowledgement from all in-sync replicas.
- Kafka idempotent producer mode is enabled.
- Outbox rows remain unpublished when Kafka is unavailable and are retried.
- `FOR UPDATE SKIP LOCKED` supports multiple API replicas publishing outbox batches concurrently.

### Consumer guarantees

- `EnableAutoCommit=false` prevents offsets from being committed before processing.
- The offset is committed only after the database write succeeds.
- `event_id` is the primary key, making database processing idempotent.
- `AutoOffsetReset=Earliest` lets a new consumer group start from the beginning.

### Ordering

Kafka preserves ordering only within a partition. The producer uses `OrderId` as the message key, so all events for the same order are routed consistently to one partition.

## Failure demonstrations

Stop Kafka, then create an order:

```bash
docker compose stop kafka
curl -X POST http://localhost:8080/api/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerName":"Failure Test","product":"Book","quantity":1}'
```

The order and outbox row are stored. Restart Kafka:

```bash
docker compose start kafka
docker compose up -d kafka-init
```

The outbox publisher eventually publishes the pending event.

Stop the consumer, create orders, and restart it:

```bash
docker compose stop order-consumer
# create one or more orders
docker compose start order-consumer
```

Kafka retains the events and the consumer resumes from its committed group offset.

## Scaling the consumer

```bash
docker compose up -d --scale order-consumer=3
```

The topic has three partitions, so up to three consumers in this group can process partitions concurrently.

## Reset the environment

This deletes PostgreSQL and Kafka volumes:

```bash
docker compose down -v --remove-orphans
```

Then restart:

```bash
docker compose up --build -d
```

## Production hardening checklist

This repository is production-oriented educational code, but production deployment should additionally provide:

- Kafka TLS/SASL authentication and authorization.
- PostgreSQL secrets from a secrets manager, not Compose literals.
- Dead-letter topic handling for permanently invalid messages.
- OpenTelemetry traces, metrics, structured log aggregation, and alerting.
- Outbox retention/cleanup and partition-aware publisher throughput tuning.
- Consumer retry policy with bounded exponential backoff.
- Readiness checks that validate Kafka and database connectivity.
- Multiple Kafka brokers/controllers with replication factor at least three.
- Schema evolution using JSON Schema, Avro, or Protobuf plus a schema registry.

## Repository layout

```text
.
├── docker-compose.yml
├── Directory.Build.props
├── DotNetKafkaPubSub.slnx
├── docker/postgres/001-init.sql
└── src
    ├── Contracts
    ├── OrderApi
    └── OrderConsumer
```

## Security package override



## NU1903 / Microsoft.OpenApi troubleshooting

This version intentionally does not reference `Microsoft.AspNetCore.OpenApi` or `Microsoft.OpenApi`. The API documentation is maintained in `docs/API.md`, avoiding the vulnerable OpenAPI dependency that can make restore fail when warnings are treated as errors.

Confirm the corrected project before rebuilding:

```bash
grep -R "Microsoft.OpenApi\|Microsoft.AspNetCore.OpenApi" -n src/OrderApi || true
```

The command should return no package references or source calls. Then rebuild with:

```bash
docker compose down -v --remove-orphans
docker compose build --no-cache --pull order-api order-consumer
docker compose up -d
```
