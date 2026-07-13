.PHONY: up down clean logs test-api consumer-rows orders outbox

up:
	docker compose up --build -d

down:
	docker compose down

clean:
	docker compose down -v --remove-orphans

logs:
	docker compose logs -f order-api order-consumer

test-api:
	curl -i -X POST http://localhost:8080/api/orders \
	  -H 'Content-Type: application/json' \
	  -d '{"customerName":"Jiten","product":"Mechanical Keyboard","quantity":2}'

orders:
	docker compose exec postgres psql -U appuser -d ordersdb -c 'TABLE orders;'

outbox:
	docker compose exec postgres psql -U appuser -d ordersdb -c 'TABLE outbox_messages;'

consumer-rows:
	docker compose exec postgres psql -U appuser -d consumerdb -c 'TABLE processed_orders;'
