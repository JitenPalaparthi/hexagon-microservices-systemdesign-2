# .NET 10 Console Kafka Example — 3-Broker KRaft Cluster

This example contains no REST API.

- `ProducerConsole` creates a topic with a chosen partition count and replication factor, then continuously publishes random JSON messages.
- `ConsumerConsole` joins or creates a Kafka consumer group by using a `group.id`, subscribes to the topic, reads continuously, and manually commits offsets.
- Docker Compose runs three Kafka 4.1.2 brokers in KRaft mode.
- Kafka UI is available at `http://localhost:8080` for inspecting brokers, topics, partitions, messages, consumer groups, offsets, and lag.

## 1. Start the Kafka cluster

```bash
docker compose down -v --remove-orphans
docker compose up -d
```

Check broker health:

```bash
docker compose ps
```

Open Kafka UI in a browser:

```text
http://localhost:8080
```

In Kafka UI, select `local-3-broker-cluster`.

## 2. Run the producer

The following command creates `orders.console.v1` with six partitions and replication factor three, then sends one random message every second continuously:

```bash
dotnet run --project src/ProducerConsole -- \
  --topic orders.console.v1 \
  --partitions 6 \
  --replication 3 \
  --interval-ms 1000
```

Use `--count 20` to stop after 20 messages. The default `--count 0` means continuous publishing.

## 3. Run a consumer

Open another terminal:

```bash
dotnet run --project src/ConsumerConsole -- \
  --topic orders.console.v1 \
  --group orders-group-a \
  --from-beginning true
```

Kafka creates the consumer group automatically when the consumer joins and begins committing offsets. The console prints the consumer group name, consumer ID, topic, partition number, offset, key, timestamp, payload, and commit status for every message.

## 4. Demonstrate partition sharing

Open a third terminal and run another consumer with the same group:

```bash
dotnet run --project src/ConsumerConsole -- \
  --topic orders.console.v1 \
  --group orders-group-a
```

Both consumers belong to `orders-group-a`, so Kafka rebalances the six partitions between them.

Run another consumer with a different group:

```bash
dotnet run --project src/ConsumerConsole -- \
  --topic orders.console.v1 \
  --group audit-group
```

Because `audit-group` is a separate group, it receives its own copy of every topic message.

## 5. Verify the topic

```bash
docker compose exec kafka1 \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka1:9092,kafka2:9092,kafka3:9092 \
  --describe --topic orders.console.v1
```

## 6. Verify consumer groups

```bash
docker compose exec kafka1 \
  /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server kafka1:9092,kafka2:9092,kafka3:9092 \
  --list
```

Describe a group and inspect lag:

```bash
docker compose exec kafka1 \
  /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server kafka1:9092,kafka2:9092,kafka3:9092 \
  --describe --group orders-group-a
```

## Producer arguments

- `--bootstrap`: broker list; default `localhost:19092,localhost:29092,localhost:39092`
- `--topic`: topic name
- `--partitions`: number of partitions; default `6`
- `--replication`: replication factor; default `3`
- `--interval-ms`: delay between messages; default `1000`
- `--count`: total messages; `0` means continuous

## Consumer arguments

- `--bootstrap`: broker list
- `--topic`: topic name
- `--group`: consumer group ID
- `--from-beginning`: `true` or `false`

Stop either console with `Ctrl+C`.


## Kafka UI

Kafka UI starts with the Compose stack and connects internally to all three brokers using:

```text
kafka1:9092,kafka2:9092,kafka3:9092
```

Open `http://localhost:8080` and use it to inspect:

- Brokers and controller status
- Topics and partition leaders
- Replicas and in-sync replicas
- Produced message payloads
- Consumer groups
- Current offsets and consumer lag

To view messages, open **Topics**, select `orders.console.v1`, then open the **Messages** tab. To inspect a group, open **Consumers** and select `orders-group-a` or the group name passed with `--group`.
