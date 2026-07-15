# C# Azure Service Bus Transactions — .NET 10 + Docker Compose

This project demonstrates a **cross-entity Azure Service Bus transaction**:

1. Receive an order from the `orders` queue using `PeekLock`.
2. Send a payment request to the `billing` queue.
3. Complete the original order message.
4. Commit both broker operations together—or roll both back together.

The application uses:

- .NET 10 console application
- `Azure.Messaging.ServiceBus` 7.20.1
- `TransactionScope` with async-flow enabled
- `EnableCrossEntityTransactions = true`
- Microsoft Azure Service Bus Emulator
- SQL Server 2022 container required by the emulator

## Architecture

```text
orders queue                    TransactionScope                    billing queue
+----------------+       +-----------------------------+       +-------------------+
| OrderCreated   | ----> | 1. Send PaymentRequested    | ----> | PaymentRequested  |
| PeekLock       |       | 2. Complete OrderCreated    |       +-------------------+
+----------------+       | 3. Complete() = COMMIT      |
                         |    no Complete() = ROLLBACK |
                         +-----------------------------+
```

Both queues are in the same emulator namespace, `sbemulatorns`.

## Prerequisites

- Docker Desktop or Docker Engine with Compose
- At least 2 GB free RAM and 5 GB free disk space

On Apple Silicon, the Compose file explicitly uses `linux/amd64` for SQL Server and the emulator. Docker Desktop may display an emulation warning; this is expected.

You do **not** need the .NET SDK installed locally because the demo application is built and run in Docker.

## Start the infrastructure

```bash
docker compose up -d sql servicebus-emulator
```

Watch startup logs:

```bash
docker compose logs -f servicebus-emulator
```

Check the emulator health endpoint:

```bash
curl http://localhost:5300/health
```

Build the C# application:

```bash
docker compose --profile tools build demo
```

Wait until AMQP operations are accepted:

```bash
docker compose --profile tools run --rm demo wait
```

## Success test

Run the complete scripted success test:

```bash
./run-success.sh
```

Or run each step manually:

```bash
docker compose --profile tools run --rm demo reset
docker compose --profile tools run --rm demo seed
docker compose --profile tools run --rm demo inspect
docker compose --profile tools run --rm demo success
docker compose --profile tools run --rm demo inspect
```

Expected final state:

```text
SUCCESS: transaction committed.
Queue state (peeked, up to 100 messages):
  orders : 0
  billing: 1
```

This proves that:

- The original `orders` message was completed.
- The new `billing` message was committed.

## Failure and rollback test

Run the complete scripted failure test:

```bash
./run-failure.sh
```

Or run each step manually:

```bash
docker compose --profile tools run --rm demo reset
docker compose --profile tools run --rm demo seed
docker compose --profile tools run --rm demo inspect
docker compose --profile tools run --rm demo failure
docker compose --profile tools run --rm demo inspect
```

The failure command performs both operations inside the transaction, then throws an exception **before** calling `TransactionScope.Complete()`.

Expected final state:

```text
EXPECTED FAILURE: Deliberate failure for rollback demonstration.
TransactionScope disposed without Complete(); both operations were rolled back.
Queue state (peeked, up to 100 messages):
  orders : 1
  billing: 0
```

This proves that:

- The billing message was not committed.
- Completing the original order was rolled back.
- The order remains available for retry.

The application abandons the order after rollback so it becomes visible immediately. If the emulator rejects that immediate abandon because of lock timing, wait for the one-minute message lock to expire and run `inspect` again.

## Retry the failed order successfully

After the failure test:

```bash
docker compose --profile tools run --rm demo success
```

Expected state:

```text
orders : 0
billing: 1
```

## Useful commands

Show current queue contents:

```bash
docker compose --profile tools run --rm demo inspect
```

Remove all test messages:

```bash
docker compose --profile tools run --rm demo reset
```

Show emulator logs:

```bash
docker compose logs servicebus-emulator
```

Show all containers:

```bash
docker compose ps
```

Stop containers:

```bash
docker compose down
```

Stop and remove the SQL volume/state:

```bash
docker compose down -v
```

## Important transaction settings

The client must enable cross-entity transactions:

```csharp
var options = new ServiceBusClientOptions
{
    EnableCrossEntityTransactions = true
};
```

Async transaction flow must be enabled:

```csharp
using var scope = new TransactionScope(
    TransactionScopeOption.Required,
    transactionOptions,
    TransactionScopeAsyncFlowOption.Enabled);
```

A transaction commits only when this is called:

```csharp
scope.Complete();
```

Disposing the scope without `Complete()` rolls back the transactional Service Bus operations.

## Production Azure Service Bus

To use a real Azure Service Bus namespace, replace `SERVICE_BUS_CONNECTION_STRING` with the namespace connection string. Both queues must be in the same namespace, and the selected Service Bus tier must support transactions.

The emulator is only for local development and testing, not production.
