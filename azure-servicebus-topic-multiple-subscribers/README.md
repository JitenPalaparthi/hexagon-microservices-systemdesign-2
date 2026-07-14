# Azure Service Bus Topic with Multiple Subscribers — .NET 10

A complete local publish/subscribe demonstration using:

- .NET 10 console applications
- `Azure.Messaging.ServiceBus`
- Azure Service Bus emulator in Docker
- SQL Server 2022, required by the emulator
- One topic: `orders-topic`
- Three independent subscriptions
- One publisher and three subscriber containers
- PeekLock processing with explicit completion

## Architecture

```text
                              +---------------------------------------+
                              | Azure Service Bus emulator            |
+----------------------+      | Topic: orders-topic                   |
| OrderPublisher       | ---> |                                       |
| .NET 10              |      |  + inventory-subscription ----------+----> Inventory subscriber
+----------------------+      |  + billing-subscription ------------+----> Billing subscriber
                              |  + notification-subscription -------+----> Notification subscriber
                              +---------------------------------------+
```

A publisher sends each event once to the topic. Service Bus creates an independent copy in every matching subscription. Completing a message in one subscription does not remove the copies held by the other subscriptions.

## Start

```bash
docker compose up --build -d
```

Or:

```bash
./scripts/start.sh
```

The publisher sends ten messages and exits. The three subscribers continue running.

## Watch all subscribers

```bash
docker compose logs -f \
  inventory-subscriber \
  billing-subscriber \
  notification-subscriber
```

For every published `MessageId`, you should see three independent `RECEIVED` and `COMPLETED` records—one per subscription.

## Publisher output

```bash
docker compose logs order-publisher
```

## Publish another batch

```bash
docker compose run --rm order-publisher
```

Custom batch:

```bash
MESSAGE_COUNT=25 SEND_INTERVAL_MS=100 docker compose run --rm order-publisher
```

## Health check

```bash
curl http://localhost:5300/health
```

## Run applications outside Docker

Start infrastructure only:

```bash
docker compose up -d mssql servicebus-emulator
```

Run subscribers in separate terminals:

```bash
SERVICEBUS_SUBSCRIPTION_NAME=inventory-subscription \
SUBSCRIBER_NAME=inventory-service \
dotnet run --project src/TopicSubscriber/TopicSubscriber.csproj
```

```bash
SERVICEBUS_SUBSCRIPTION_NAME=billing-subscription \
SUBSCRIBER_NAME=billing-service \
dotnet run --project src/TopicSubscriber/TopicSubscriber.csproj
```

```bash
SERVICEBUS_SUBSCRIPTION_NAME=notification-subscription \
SUBSCRIBER_NAME=notification-service \
dotnet run --project src/TopicSubscriber/TopicSubscriber.csproj
```

Then publish:

```bash
dotnet run --project src/OrderPublisher/OrderPublisher.csproj
```

## Message flow

1. `OrderPublisher` serializes an `OrderCreated` event.
2. It sends the event once to `orders-topic`.
3. Service Bus places one copy into each subscription.
4. Every subscriber receives from its own subscription.
5. Each subscriber processes and explicitly completes its own copy.
6. Failure in one subscriber does not prevent the other subscribers from processing.
7. An abandoned message is redelivered only within the subscription where processing failed.
8. After five failed deliveries, that subscription moves its copy to its dead-letter queue.

## Queue versus topic

| Queue | Topic with subscriptions |
|---|---|
| Point-to-point | Publish/subscribe |
| One message is processed by one competing consumer | Every subscription receives its own copy |
| Consumers share one backlog | Each subscription has an independent backlog |
| Suitable for work distribution | Suitable for event broadcasting |

## Configuration

The emulator entities are declared in `config/Config.json`.

Environment variables:

| Name | Default | Description |
|---|---|---|
| `SERVICEBUS_CONNECTION_STRING` | Local emulator string | Service Bus connection |
| `SERVICEBUS_TOPIC_NAME` | `orders-topic` | Topic used by publisher/subscribers |
| `SERVICEBUS_SUBSCRIPTION_NAME` | `inventory-subscription` | Subscription read by a subscriber |
| `SUBSCRIBER_NAME` | Subscription name | Friendly logging identity |
| `MESSAGE_COUNT` | `10` | Events sent per publisher execution |
| `SEND_INTERVAL_MS` | `700` | Delay between published events |

## Stop

```bash
docker compose down --remove-orphans
```

To reset all emulator state, recreate the containers. The emulator is intended for local development and testing, not production use.

## Apple Silicon note

The Compose file selects `linux/amd64` for SQL Server and the Service Bus emulator. Docker Desktop may use emulation on Apple Silicon. Enable Rosetta/x86 emulation if Docker requests it.
