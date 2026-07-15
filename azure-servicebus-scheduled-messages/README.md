# Azure Service Bus Scheduled Messages — .NET 10

This project demonstrates delayed/scheduled queue delivery using:

- .NET 10 console applications
- `Azure.Messaging.ServiceBus`
- Azure Service Bus emulator
- SQL Server 2022, required by the emulator
- Docker Compose
- Scheduled-message cancellation

## Message flow

```text
OrderScheduler
      |
      | ScheduleMessageAsync(message, futureUtcTime)
      v
scheduled-orders queue
      |
      | Hidden from normal receivers until scheduled time
      v
OrderConsumer
      |
      | CompleteMessageAsync
      v
Message removed
```

Cancellation flow:

```text
ScheduledMessageCanceller
      |
      | Schedule message and receive sequence number
      |
      | CancelScheduledMessageAsync(sequenceNumber)
      v
Message deleted before delivery
```

## Project structure

```text
.
├── config/
│   └── Config.json
├── src/
│   ├── OrderScheduler/
│   ├── OrderConsumer/
│   └── ScheduledMessageCanceller/
├── docker-compose.yml
├── AzureServiceBusScheduledMessages.slnx
└── README.md
```

## Prerequisites

- Docker Desktop
- Approximately 2 GB or more free memory
- On an Apple Silicon Mac, enable Docker Desktop's Rosetta/x86-64 emulation because the SQL Server Linux image uses `linux/amd64`.
- The .NET 10 SDK is optional when everything is run through Docker.

Verify amd64 emulation on Apple Silicon:

```bash
docker run --rm --platform linux/amd64 alpine uname -m
```

Expected result:

```text
x86_64
```

## Run the complete example

Clean any previous run:

```bash
docker compose down -v --remove-orphans
```

Build and start:

```bash
docker compose up --build -d
```

Check status:

```bash
docker compose ps
```

Watch the consumer:

```bash
docker compose logs -f order-consumer
```

View scheduled-message producer output:

```bash
docker compose logs order-scheduler
```

The default schedule is:

| Message | Delay |
|---|---:|
| 1 | 10 seconds |
| 2 | 20 seconds |
| 3 | 30 seconds |

The consumer starts before these messages are due. It receives each message only when its scheduled enqueue time arrives.

## Schedule another batch

```bash
docker compose run --rm order-scheduler
```

Customize the timing:

```bash
MESSAGE_COUNT=5 \
FIRST_DELAY_SECONDS=15 \
DELAY_BETWEEN_MESSAGES_SECONDS=5 \
docker compose run --rm order-scheduler
```

This schedules five messages at approximately 15, 20, 25, 30, and 35 seconds from the producer's run time.

## Run the cancellation demonstration

The cancellation service is behind a Compose profile so it does not run during the normal demonstration.

```bash
docker compose --profile cancel-demo run --rm scheduled-message-canceller
```

Defaults:

- Schedule the message 30 seconds into the future.
- Wait 5 seconds.
- Cancel using the returned sequence number.
- The consumer must not receive that message.

Customize it:

```bash
SCHEDULE_DELAY_SECONDS=60 \
CANCEL_AFTER_SECONDS=10 \
docker compose --profile cancel-demo run --rm scheduled-message-canceller
```

`CANCEL_AFTER_SECONDS` must be lower than `SCHEDULE_DELAY_SECONDS`.

## Important API calls

### Schedule one message

```csharp
long sequenceNumber = await sender.ScheduleMessageAsync(
    message,
    DateTimeOffset.UtcNow.AddSeconds(30));
```

The returned `sequenceNumber` identifies the scheduled broker message and is needed for cancellation.

### Cancel before delivery

```csharp
await sender.CancelScheduledMessageAsync(sequenceNumber);
```

Cancellation must happen before the scheduled message becomes active.

### Consumer

The consumer does not need special scheduled-message code:

```csharp
ServiceBusProcessor processor = client.CreateProcessor(queueName);
```

It receives the message like any normal queue message after the scheduled enqueue time.

## Docker Compose settings

### `mssql`

```yaml
image: mcr.microsoft.com/mssql/server:2022-latest
platform: linux/amd64
```

SQL Server is the emulator's backend database. `platform` is especially relevant on Apple Silicon.

```yaml
MSSQL_SA_PASSWORD: "Local_ServiceBus_2026!"
```

This must be identical in the SQL Server and Service Bus emulator services.

```yaml
healthcheck:
```

The health check runs `SELECT 1`. The emulator starts only after SQL Server is ready.

```yaml
start_period: 120s
```

SQL Server can start slowly under amd64 emulation on Apple Silicon.

### `servicebus-emulator`

```yaml
SQL_SERVER: mssql
```

This is the Docker DNS service name of SQL Server.

```yaml
SQL_WAIT_INTERVAL: "15"
```

The emulator waits between SQL readiness attempts.

```yaml
ports:
  - "5672:5672"
  - "5300:5300"
```

- `5672`: AMQP message traffic
- `5300`: management and health endpoint

Check health from the host:

```bash
curl http://localhost:5300/health
```

### Connection string

Applications running in the same Docker network use:

```text
Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

`servicebus-emulator` is the Docker network alias. A .NET application running directly on the host should use `localhost` instead:

```text
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

### Producer timing settings

```yaml
MESSAGE_COUNT: "3"
FIRST_DELAY_SECONDS: "10"
DELAY_BETWEEN_MESSAGES_SECONDS: "10"
```

- `MESSAGE_COUNT`: number of messages scheduled
- `FIRST_DELAY_SECONDS`: delay for the first message
- `DELAY_BETWEEN_MESSAGES_SECONDS`: additional spacing for each next message

Formula:

```text
delay(n) = FIRST_DELAY_SECONDS + (n - 1) × DELAY_BETWEEN_MESSAGES_SECONDS
```

### Consumer settings

```yaml
MAX_CONCURRENT_CALLS: "2"
PREFETCH_COUNT: "0"
```

- `MAX_CONCURRENT_CALLS`: maximum parallel message handlers.
- `PREFETCH_COUNT`: messages cached locally in advance. Zero keeps the timing demonstration easier to observe.
- `AutoCompleteMessages = false`: application explicitly completes messages.
- `PeekLock`: message is locked while processing and removed only after completion.

### Queue configuration

`config/Config.json` creates `scheduled-orders`.

```json
"DefaultMessageTimeToLive": "PT1H"
```

The emulator's supported message TTL maximum is one hour.

```json
"LockDuration": "PT1M"
```

A received message is initially locked for one minute.

```json
"MaxDeliveryCount": 5
```

After repeated failed deliveries, the message is moved to the queue's dead-letter subqueue.

```json
"DeadLetteringOnMessageExpiration": true
```

Expired messages are dead-lettered instead of silently removed.

## Running applications outside Docker

Start only the infrastructure:

```bash
docker compose up -d mssql servicebus-emulator
```

Set host connection variables:

```bash
export SERVICEBUS_CONNECTION_STRING='Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
export SERVICEBUS_QUEUE_NAME='scheduled-orders'
```

Run the consumer:

```bash
dotnet run --project src/OrderConsumer
```

In another terminal, run the scheduler:

```bash
dotnet run --project src/OrderScheduler
```

Run cancellation:

```bash
dotnet run --project src/ScheduledMessageCanceller
```

## Troubleshooting

### SQL Server remains unhealthy

```bash
docker compose logs --tail=200 mssql
```

Inspect health-check output:

```bash
docker inspect \
  azure-servicebus-dotnet10-scheduled-messages-mssql-1 \
  --format='{{range .State.Health.Log}}{{println "Exit:" .ExitCode}}{{println .Output}}{{end}}'
```

On Apple Silicon, confirm Docker Desktop Rosetta support and allow sufficient memory.

### Emulator starts but applications cannot connect

```bash
docker compose logs --tail=200 servicebus-emulator
curl http://localhost:5300/health
```

Ensure containerized applications use `servicebus-emulator`, not `localhost`, in the connection string.

### Queue configuration changed but is not reflected

Restart the emulator. Configuration changes are loaded at emulator startup:

```bash
docker compose down -v
docker compose up --build -d
```

### Scheduled message appears late

Scheduled enqueue time means the message becomes eligible for delivery at or after that UTC time. It is not a real-time deadline guarantee; actual receipt can occur slightly later due to broker and consumer scheduling.

## Stop

```bash
docker compose down
```

Remove containers and local emulator data:

```bash
docker compose down -v
```

The emulator is intended only for development and testing. Its data and entities do not persist across container restarts.
