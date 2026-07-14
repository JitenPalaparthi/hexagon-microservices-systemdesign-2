# Step-by-Step Run and Verification Guide

## 1. Reset old containers and volumes

Run this from the project directory:

```bash
docker compose down -v --remove-orphans
```

This is important because PostgreSQL initialization scripts and Kafka KRaft metadata run only against fresh volumes.

## 2. Build the .NET services

```bash
docker compose build --no-cache order-api order-consumer
```

Both images must build successfully before continuing.

## 3. Start PostgreSQL and Kafka only

```bash
docker compose up -d postgres kafka
```

Check their health:

```bash
docker compose ps
```

Wait until both show `healthy`.

Kafka logs:

```bash
docker compose logs --tail=200 kafka
```

## 4. Create the Kafka topic

Run the one-shot initializer:

```bash
docker compose up kafka-init
```

Expected final output:

```text
Kafka initialization completed successfully.
```

The initializer should exit with code 0. `Exited (0)` is normal for this one-shot service.

```bash
docker compose ps -a kafka-init
```

## 5. Verify the topic directly

List topics:

```bash
docker compose exec kafka \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --list
```

Expected:

```text
orders.created.v1
```

Describe it:

```bash
docker compose exec kafka \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --describe \
  --topic orders.created.v1
```

Expected: 3 partitions and replication factor 1.

## 6. Start the API and consumer

```bash
docker compose up -d order-api order-consumer
```

Check all services:

```bash
docker compose ps -a
```

Follow logs:

```bash
docker compose logs -f order-api order-consumer
```

In another terminal, test the API health endpoint:

```bash
curl -i http://localhost:8080/health
```

Expected HTTP status: `200 OK`.

## 7. Produce an event through the REST API

```bash
curl -sS -X POST http://localhost:8080/api/orders \
  -H 'Content-Type: application/json' \
  -d '{
    "customerName": "Jiten",
    "product": "Mechanical Keyboard",
    "quantity": 2
  }'
```

Copy the returned `id` value.

The initial status can be `PendingPublication` because the outbox publisher runs asynchronously.

## 8. Confirm the producer published the event

Replace `ORDER_ID` with the returned ID:

```bash
curl -sS http://localhost:8080/api/orders/ORDER_ID
```

After a short delay, the response must contain:

```json
"eventStatus": "Published"
```

Check the API publisher log:

```bash
docker compose logs --tail=200 order-api
```

Look for a log similar to:

```text
Published event ... to orders.created.v1
```

Check the outbox table:

```bash
docker compose exec postgres \
  psql -U appuser -d ordersdb -c \
  "SELECT event_id, aggregate_id, published_at_utc, kafka_partition, kafka_offset FROM outbox_messages ORDER BY occurred_at_utc DESC;"
```

`published_at_utc`, `kafka_partition`, and `kafka_offset` must not be null.

## 9. Read the Kafka message manually

```bash
docker compose exec kafka \
  /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server kafka:9092 \
  --topic orders.created.v1 \
  --from-beginning \
  --max-messages 1
```

This proves that the API produced a Kafka message.

## 10. Confirm the consumer processed the message

Check consumer logs:

```bash
docker compose logs --tail=200 order-consumer
```

Look for:

```text
Processed order ...
```

Query the consumer database:

```bash
docker compose exec postgres \
  psql -U appuser -d consumerdb -c \
  "SELECT event_id, order_id, customer_name, product, quantity, processed_at_utc FROM processed_orders ORDER BY processed_at_utc DESC;"
```

The newly created order must appear here.

## 11. Run the automated end-to-end verification

The repository includes a verification script:

```bash
./scripts/verify-pubsub.sh
```

It checks the topic, calls the REST API, confirms outbox publication, reads Kafka, and verifies the consumer database.

## 12. Optional Kafka UI

Start the UI-enabled Compose file:

```bash
docker compose -f docker-compose-with-ui.yaml up --build -d
```

Open:

- Kafka UI: `http://localhost:8084`
- Adminer: `http://localhost:8081`
- REST API: `http://localhost:8080`

Adminer connection:

- System: PostgreSQL
- Server: postgres
- Username: appuser
- Password: apppassword
- Database: ordersdb or consumerdb

## Troubleshooting commands

```bash
docker compose ps -a
docker compose logs --tail=300 kafka
docker compose logs --tail=300 kafka-init
docker compose logs --tail=300 order-api
docker compose logs --tail=300 order-consumer
```

If initialization still reflects an old configuration, reset all data and rebuild:

```bash
docker compose down -v --remove-orphans
docker compose build --no-cache
docker compose up -d
```
