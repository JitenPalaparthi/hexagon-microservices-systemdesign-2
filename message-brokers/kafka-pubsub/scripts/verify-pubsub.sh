#!/usr/bin/env bash
set -euo pipefail

TOPIC="orders.created.v1"
API_URL="http://localhost:8080"

echo "[1/7] Checking container status"
docker compose ps -a

echo "[2/7] Verifying Kafka topic"
docker compose exec -T kafka \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --describe \
  --topic "$TOPIC"

echo "[3/7] Creating an order through the REST API"
RESPONSE="$(curl -fsS -X POST "$API_URL/api/orders" \
  -H 'Content-Type: application/json' \
  -d '{"customerName":"Jiten","product":"Mechanical Keyboard","quantity":2}')"

echo "$RESPONSE"
ORDER_ID="$(printf '%s' "$RESPONSE" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')"

if [ -z "$ORDER_ID" ]; then
  echo "Could not extract order ID from API response" >&2
  exit 1
fi

echo "[4/7] Waiting for outbox publication"
for _ in $(seq 1 20); do
  ORDER="$(curl -fsS "$API_URL/api/orders/$ORDER_ID")"
  echo "$ORDER"
  echo "$ORDER" | grep -q '"eventStatus":"Published"' && break
  sleep 1
done

echo "[5/7] Reading the produced Kafka event"
docker compose exec -T kafka \
  timeout 10 /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server kafka:9092 \
  --topic "$TOPIC" \
  --from-beginning \
  --max-messages 1 || true

echo "[6/7] Checking the producer outbox row"
docker compose exec -T postgres \
  psql -U appuser -d ordersdb -c \
  "SELECT event_id, aggregate_id, published_at_utc, kafka_partition, kafka_offset FROM outbox_messages WHERE aggregate_id = '$ORDER_ID';"

echo "[7/7] Checking the consumer database"
for _ in $(seq 1 20); do
  COUNT="$(docker compose exec -T postgres psql -U appuser -d consumerdb -tAc "SELECT COUNT(*) FROM processed_orders WHERE order_id = '$ORDER_ID';")"
  if [ "$COUNT" = "1" ]; then
    docker compose exec -T postgres psql -U appuser -d consumerdb -c \
      "SELECT event_id, order_id, customer_name, product, quantity, processed_at_utc FROM processed_orders WHERE order_id = '$ORDER_ID';"
    echo "Pub/Sub verification completed successfully."
    exit 0
  fi
  sleep 1
done

echo "Consumer did not persist the order within the expected time." >&2
docker compose logs --no-color --tail=200 order-consumer
exit 1
