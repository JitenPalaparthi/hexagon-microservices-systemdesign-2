# Apache Kafka — Detailed Concepts and Internal Architecture

## Brokers, Topics, Partitions, Messages, Offsets, Consumers, Replication, and KRaft

This guide explains the core Kafka concepts in detail, using practical examples and ASCII diagrams.

---

# Table of Contents

1. [What Kafka Is](#1-what-kafka-is)
2. [Kafka Architecture Overview](#2-kafka-architecture-overview)
3. [Producer](#3-producer)
4. [Message or Record](#4-message-or-record)
5. [Topic](#5-topic)
6. [Partition](#6-partition)
7. [Broker](#7-broker)
8. [Offset](#8-offset)
9. [Consumer](#9-consumer)
10. [Consumer Group](#10-consumer-group)
11. [How Kafka Tracks What a Consumer Has Read](#11-how-kafka-tracks-what-a-consumer-has-read)
12. [Committed Offset, Current Position, and Lag](#12-committed-offset-current-position-and-lag)
13. [Offset Commit Strategies](#13-offset-commit-strategies)
14. [Consumer Group Coordinator](#14-consumer-group-coordinator)
15. [Consumer Rebalancing](#15-consumer-rebalancing)
16. [Replication](#16-replication)
17. [Leader and Follower Replicas](#17-leader-and-follower-replicas)
18. [ISR — In-Sync Replicas](#18-isr--in-sync-replicas)
19. [Producer Acknowledgements](#19-producer-acknowledgements)
20. [Delivery Guarantees](#20-delivery-guarantees)
21. [Ordering](#21-ordering)
22. [Retention](#22-retention)
23. [Log Compaction](#23-log-compaction)
24. [Kafka Storage Internals](#24-kafka-storage-internals)
25. [KRaft](#25-kraft)
26. [KRaft Controller Quorum](#26-kraft-controller-quorum)
27. [End-to-End Message Flow](#27-end-to-end-message-flow)
28. [Your Docker Compose Configuration](#28-your-docker-compose-configuration)
29. [Important Kafka Commands](#29-important-kafka-commands)
30. [Common Misunderstandings](#30-common-misunderstandings)
31. [Production Recommendations](#31-production-recommendations)
32. [Glossary](#32-glossary)

---

# 1. What Kafka Is

Apache Kafka is a distributed event-streaming platform.

It is designed to:

- Receive events from applications.
- Store those events durably.
- Distribute those events across multiple servers.
- Allow many independent consumers to read the same events.
- Allow consumers to replay old events.
- Process very high volumes of data.
- Continue operating when servers fail.

Kafka can be viewed as a distributed append-only log.

```text
Producer
   |
   | sends event
   v
Kafka topic
   |
   | stores event
   v
Consumer
```

Unlike many traditional message queues, Kafka does not normally remove a message immediately after a consumer reads it.

Kafka retains records according to a configured retention policy.

Because records remain available, several independent consumer groups can read the same event.

Example:

```text
orders.created.v1
       |
       +----> Inventory Service
       |
       +----> Notification Service
       |
       +----> Analytics Service
       |
       +----> Fraud Detection Service
```

Each service can have its own consumer group and maintain its own reading position.

---

# 2. Kafka Architecture Overview

A Kafka system usually contains the following components:

```text
+------------------+
| Producer         |
+------------------+
         |
         | publish records
         v
+------------------------------------------------------+
| Kafka Cluster                                        |
|                                                      |
|  +------------+  +------------+  +------------+      |
|  | Broker 1   |  | Broker 2   |  | Broker 3   |      |
|  +------------+  +------------+  +------------+      |
|                                                      |
|  Topics -> Partitions -> Replicas                     |
|                                                      |
|  +-----------------------------------------------+   |
|  | KRaft Controller Quorum                       |   |
|  | Controller 1 | Controller 2 | Controller 3   |   |
|  +-----------------------------------------------+   |
+------------------------------------------------------+
         |
         | fetch records
         v
+------------------+
| Consumer Group   |
+------------------+
```

The major responsibilities are:

| Component | Main responsibility |
|---|---|
| Producer | Publishes records |
| Broker | Stores and serves records |
| Topic | Logical stream of records |
| Partition | Ordered append-only log |
| Consumer | Reads records |
| Consumer group | Shares processing work |
| KRaft controller | Manages cluster metadata |
| Replica | Copy of a partition |
| Offset | Position of a record inside a partition |

---

# 3. Producer

A producer is an application that publishes records to Kafka.

Examples:

- An Order API publishes `OrderCreated`.
- A Payment API publishes `PaymentCompleted`.
- A sensor publishes temperature readings.
- A website publishes click events.
- A banking system publishes transaction events.

Example producer flow:

```text
Order API
   |
   | creates order
   |
   | serializes event
   v
Kafka producer client
   |
   | sends to topic
   v
orders.created.v1
```

## 3.1 Producer responsibilities

A Kafka producer generally handles:

- Serialization
- Partition selection
- Batching
- Compression
- Retries
- Acknowledgement handling
- Idempotence
- Error handling

## 3.2 Serialization

Kafka stores bytes.

A producer therefore converts an application object into bytes.

```text
C# OrderCreated object
        |
        v
JSON serializer
        |
        v
UTF-8 byte array
        |
        v
Kafka
```

Example event:

```json
{
  "orderId": "ORD-1001",
  "customerId": "CUS-25",
  "amount": 1500.50,
  "status": "CREATED"
}
```

Kafka itself does not understand the business meaning of this JSON.

## 3.3 Batching

Kafka producers usually combine multiple records into batches.

```text
Record 1
Record 2
Record 3
Record 4
    |
    v
Producer batch
    |
    v
One network request
```

Batching improves throughput because fewer network requests are required.

Important producer settings include:

```text
batch.size
linger.ms
compression.type
```

## 3.4 Retries

If a producer request temporarily fails, the producer may retry.

Without proper configuration, retries can cause duplicate records.

Modern Kafka producers support idempotence to prevent duplicate writes caused by retries.

---

# 4. Message or Record

Kafka officially calls a message a **record**.

The terms message, event, and record are often used interchangeably, but there are subtle differences:

- **Message** is a general messaging term.
- **Event** represents something that happened.
- **Record** is Kafka's technical term.

A Kafka record can contain:

```text
+--------------------------------------------------+
| Key                                              |
+--------------------------------------------------+
| Value                                            |
+--------------------------------------------------+
| Headers                                          |
+--------------------------------------------------+
| Timestamp                                        |
+--------------------------------------------------+
| Topic                                            |
+--------------------------------------------------+
| Partition                                        |
+--------------------------------------------------+
| Offset                                           |
+--------------------------------------------------+
```

## 4.1 Key

The key is optional.

Example:

```text
Key = customer-100
```

The key is commonly used to ensure that related records go to the same partition.

Example:

```text
customer-100 -> CustomerCreated
customer-100 -> AddressChanged
customer-100 -> CustomerBlocked
```

When the same key is used, these records normally go to the same partition.

This helps preserve order for that customer.

## 4.2 Value

The value contains the main event data.

Example:

```json
{
  "orderId": "ORD-1001",
  "amount": 4999,
  "currency": "INR"
}
```

## 4.3 Headers

Headers contain metadata.

Example:

```text
correlation-id = 4a42c3
event-version  = 1
content-type   = application/json
trace-id       = a8cc091
source-service = order-api
```

Headers are useful for:

- Distributed tracing
- Event versioning
- Content type identification
- Tenant information
- Correlation IDs
- Routing metadata

## 4.4 Timestamp

A Kafka record contains a timestamp.

The timestamp may represent:

- Producer creation time
- Broker append time

Common timestamp policies are:

```text
CreateTime
LogAppendTime
```

## 4.5 Record address

A record is located using:

```text
topic + partition + offset
```

Example:

```text
Topic:     orders.created.v1
Partition: 2
Offset:    145
```

This combination identifies one position in Kafka.

---

# 5. Topic

A topic is a named logical stream of records.

Examples:

```text
orders.created.v1
orders.cancelled.v1
payments.completed.v1
inventory.reserved.v1
users.registered.v1
```

A topic normally represents one kind of event or data stream.

## 5.1 Topic as a logical category

```text
Topic: orders.created.v1

OrderCreated event 1
OrderCreated event 2
OrderCreated event 3
OrderCreated event 4
```

A topic is not physically stored as one single file.

It is divided into partitions.

```text
orders.created.v1
       |
       +---- Partition 0
       +---- Partition 1
       +---- Partition 2
```

## 5.2 Why use separate topics?

Separate topics provide:

- Logical separation
- Independent retention policies
- Independent permissions
- Independent partition counts
- Independent consumer subscriptions
- Independent compaction policies

Example:

```text
orders.created.v1
orders.shipped.v1
orders.cancelled.v1
```

## 5.3 Topic naming

A good topic name should express:

- Domain
- Entity
- Event
- Version

Example:

```text
commerce.orders.created.v1
```

Possible naming structure:

```text
<domain>.<entity>.<event>.<version>
```

## 5.4 Topic configuration

Important topic settings include:

```text
partitions
replication.factor
retention.ms
retention.bytes
cleanup.policy
min.insync.replicas
segment.bytes
```

---

# 6. Partition

A partition is an ordered, append-only log inside a topic.

Example:

```text
Partition 0

Offset     Record
------     ------------------
0          OrderCreated A
1          OrderCreated B
2          OrderCreated C
3          OrderCreated D
```

New records are appended at the end.

Kafka does not insert a record between offsets 1 and 2.

## 6.1 Why partitions exist

Partitions provide:

- Parallel processing
- Horizontal scalability
- Distributed storage
- Fault tolerance through replication
- Ordering within a partition

## 6.2 Partition-level ordering

Kafka guarantees ordering only inside one partition.

```text
Partition 0:
A -> B -> C -> D
```

Kafka does not guarantee total order across several partitions.

```text
Partition 0: A -> C -> E
Partition 1: B -> D -> F
```

There is no guaranteed global order between A, B, C, D, E, and F.

## 6.3 Partitions and parallelism

Suppose a topic has three partitions:

```text
P0
P1
P2
```

A consumer group can process all three in parallel:

```text
Consumer 1 -> P0
Consumer 2 -> P1
Consumer 3 -> P2
```

A fourth consumer in the same group may remain idle:

```text
Consumer 4 -> no partition
```

The number of partitions limits the maximum active partition-level parallelism in a traditional consumer group.

## 6.4 One consumer can own multiple partitions

```text
Partitions: P0 P1 P2 P3 P4 P5
Consumers: C1 C2

C1 -> P0, P2, P4
C2 -> P1, P3, P5
```

## 6.5 Key-based partitioning

A producer often uses the key to select a partition.

Conceptually:

```text
partition = hash(key) % partition_count
```

Example:

```text
key = order-1001
hash = 87543
partition count = 3

87543 % 3 = 0
```

The record goes to partition 0.

## 6.6 Important warning when increasing partitions

Suppose a key maps using:

```text
hash(key) % 3
```

After increasing partitions:

```text
hash(key) % 6
```

The same key may map to a different partition.

Therefore, increasing a topic's partition count can affect key-based ordering.

## 6.7 Hot partitions

A hot partition receives much more traffic than other partitions.

Example:

```text
P0 -> 90% of records
P1 -> 5%
P2 -> 5%
```

This often happens when:

- One key dominates traffic.
- The partitioning strategy is poor.
- Keys are not evenly distributed.

Hot partitions can cause:

- High broker load
- High consumer lag
- Uneven disk usage
- Reduced throughput

---

# 7. Broker

A broker is one Kafka server process.

A broker stores partition data and serves client requests.

A broker can:

- Accept producer writes
- Serve consumer reads
- Store partition leaders
- Store partition followers
- Replicate data
- Coordinate consumer groups
- Respond to metadata requests
- Maintain logs and indexes

## 7.1 Broker and cluster

```text
Broker = one Kafka server
Cluster = multiple Kafka brokers working together
```

Example:

```text
Kafka Cluster
   |
   +---- Broker 1
   +---- Broker 2
   +---- Broker 3
```

## 7.2 Broker ID

Each broker has a unique node ID.

Example:

```yaml
KAFKA_NODE_ID: 1
```

In a multi-node cluster:

```text
Broker 1 -> node.id=1
Broker 2 -> node.id=2
Broker 3 -> node.id=3
```

## 7.3 Broker stores partitions

Example without replication:

```text
Broker 1 -> Topic A Partition 0
Broker 2 -> Topic A Partition 1
Broker 3 -> Topic A Partition 2
```

Example with replication:

```text
Broker 1 -> P0 leader, P1 follower
Broker 2 -> P1 leader, P2 follower
Broker 3 -> P2 leader, P0 follower
```

## 7.4 Bootstrap server

A bootstrap server is the first broker address used by a Kafka client.

Example:

```text
kafka1:9092,kafka2:9092,kafka3:9092
```

The client connects to one reachable broker and asks for cluster metadata.

```text
Producer
   |
   | connect
   v
Bootstrap broker
   |
   | returns metadata
   v
Producer learns:
P0 leader = Broker 2
P1 leader = Broker 3
P2 leader = Broker 1
```

The producer then sends directly to the partition leader.

The bootstrap server is not a permanent proxy.

---

# 8. Offset

An offset is the position of a record inside a partition.

```text
Partition 0

Offset 0 -> Record A
Offset 1 -> Record B
Offset 2 -> Record C
Offset 3 -> Record D
```

Each partition has its own offset sequence.

```text
Partition 0: 0, 1, 2, 3...
Partition 1: 0, 1, 2, 3...
Partition 2: 0, 1, 2, 3...
```

## 8.1 Offset is not globally unique

These are different positions:

```text
Topic A / Partition 0 / Offset 10
Topic A / Partition 1 / Offset 10
```

## 8.2 Offset is assigned by Kafka

The producer normally does not choose the offset.

Kafka appends the record and assigns the next offset.

## 8.3 Offset is not a business ID

```text
Business ID = ORD-1001
Kafka position = partition 2, offset 44
```

The same business event may appear multiple times at different offsets.

## 8.4 Offset gaps

Applications should not assume offsets are always perfectly consecutive forever.

Transactions, control records, retention, compaction, and internal mechanics can make application-visible assumptions about strict continuity unsafe.

The correct rule is:

> Treat offsets as ordered positions, not as a count of business messages.

---

# 9. Consumer

A consumer is an application that reads records from Kafka.

Examples:

- Inventory service
- Notification service
- Fraud detection service
- Analytics processor
- Database writer

A consumer usually performs this loop:

```text
1. Subscribe to topic
2. Join consumer group
3. Receive partition assignments
4. Poll Kafka
5. Deserialize records
6. Process records
7. Commit offsets
8. Poll again
```

## 9.1 Poll model

Kafka consumers pull records from brokers.

```text
Consumer ---- fetch request ----> Broker
Consumer <--- record batch ------ Broker
```

Kafka does not continuously push messages to the consumer.

This pull model allows the consumer to control its processing rate.

## 9.2 Deserialization

Kafka returns bytes.

The consumer converts those bytes into an application object.

```text
Kafka bytes
    |
    v
JSON deserializer
    |
    v
OrderCreated object
```

## 9.3 Consumer responsibilities

A consumer must handle:

- Deserialization
- Business processing
- Offset commits
- Failures
- Retries
- Duplicate events
- Rebalances
- Poison messages
- Graceful shutdown

---

# 10. Consumer Group

A consumer group is a collection of consumers that cooperate to process records.

All consumers in the group use the same group ID.

Example:

```text
group.id = order-processor-v1
```

## 10.1 Work sharing

Suppose a topic has three partitions:

```text
P0
P1
P2
```

With three consumers in one group:

```text
C1 -> P0
C2 -> P1
C3 -> P2
```

Each partition is processed by one consumer in the group at a time.

## 10.2 Multiple groups

Different groups read the same topic independently.

```text
orders.created.v1
      |
      +---- inventory-service group
      +---- notification-service group
      +---- analytics-service group
```

Each group has its own offsets.

## 10.3 Queue-like behavior

Inside one consumer group, consumers share work.

```text
One topic + one group + many consumers
```

This resembles competing consumers in a queue.

## 10.4 Publish-subscribe behavior

Different groups all receive the records.

```text
One topic + many groups
```

This provides publish-subscribe behavior.

Kafka supports both models at the same time.

## 10.5 More consumers than partitions

```text
Topic partitions = 3
Consumers in group = 5
```

Possible result:

```text
C1 -> P0
C2 -> P1
C3 -> P2
C4 -> idle
C5 -> idle
```

Adding consumers beyond the number of partitions does not increase parallelism.

---

# 11. How Kafka Tracks What a Consumer Has Read

Kafka does not mark a message as globally read.

Kafka tracks progress for each combination of:

```text
consumer group + topic + partition
```

Example:

```text
Group: order-processor-v1
Topic: orders.created.v1

Partition 0 -> committed offset 120
Partition 1 -> committed offset 83
Partition 2 -> committed offset 201
```

This is how Kafka knows where the consumer group should resume.

## 11.1 Important meaning of committed offset

A committed offset normally means:

> The next offset the group should read.

Example:

```text
Committed offset = 10
```

This usually means:

```text
Offsets 0 through 9 are considered complete.
Offset 10 should be read next.
```

## 11.2 Where offsets are stored

Kafka stores consumer group offsets in an internal topic:

```text
__consumer_offsets
```

This is a compacted internal Kafka topic.

Conceptually, an offset entry contains:

```text
Group: order-processor-v1
Topic: orders.created.v1
Partition: 1
Committed offset: 83
```

## 11.3 Consumer restart

Before failure:

```text
P0 committed = 120
P1 committed = 83
P2 committed = 201
```

After restart, the group resumes from those offsets.

```text
P0 -> read from 120
P1 -> read from 83
P2 -> read from 201
```

## 11.4 Different groups have different progress

```text
inventory-service:
P0 -> 150

notification-service:
P0 -> 132

analytics-service:
P0 -> 80
```

All groups read the same partition independently.

---

# 12. Committed Offset, Current Position, and Lag

These three concepts are different.

## 12.1 Current position

The current position is where the live consumer will fetch next.

Example:

```text
Current position = 150
```

The consumer has already fetched records before 150.

## 12.2 Committed offset

The committed offset is the durable recovery point stored for the group.

Example:

```text
Committed offset = 140
```

If the consumer crashes, the group resumes from 140.

## 12.3 Difference between position and commit

```text
Current position   = 150
Committed offset   = 140
```

Records 140 through 149 have been fetched but not durably committed.

If the consumer crashes, those records may be read again.

## 12.4 Log end offset

The log end offset is the next position at the end of the partition.

Example:

```text
Existing offsets: 0 through 199
Log end offset: 200
```

## 12.5 Consumer lag

Conceptually:

```text
Lag = log end offset - committed offset
```

Example:

```text
Log end offset = 200
Committed offset = 150
Lag = 50
```

The consumer group is 50 records behind for that partition.

## 12.6 Lag is per partition

```text
P0 lag = 10
P1 lag = 200
P2 lag = 0
```

Even when total lag looks acceptable, one hot partition may be severely delayed.

---

# 13. Offset Commit Strategies

Offset commit strategy strongly affects delivery guarantees.

## 13.1 Auto commit

With auto commit enabled, Kafka periodically commits offsets.

Example configuration:

```text
enable.auto.commit=true
auto.commit.interval.ms=5000
```

Potential problem:

```text
1. Consumer polls offsets 10-19.
2. Auto commit stores offset 20.
3. Application has processed only 10-14.
4. Consumer crashes.
5. Consumer restarts at 20.
6. Offsets 15-19 may be skipped.
```

## 13.2 Manual commit after processing

Safer at-least-once pattern:

```text
1. Poll records.
2. Process records.
3. Commit after successful processing.
```

Example:

```text
Read 10, 11, 12
Process successfully
Commit 13
```

If the process crashes before committing, the records may be delivered again.

## 13.3 Commit before processing

```text
1. Poll records.
2. Commit offset.
3. Process records.
```

This can produce at-most-once behavior.

If processing fails after the commit, the records may not be retried.

## 13.4 Synchronous commit

A synchronous commit waits for the broker response.

Advantages:

- Easier failure detection
- Stronger confirmation

Disadvantages:

- More latency
- Lower throughput if used too frequently

## 13.5 Asynchronous commit

An asynchronous commit does not block while waiting for the result.

Advantages:

- Better throughput
- Lower blocking latency

Disadvantages:

- More complex error handling
- Commit completion can arrive out of order
- Final shutdown commit needs care

## 13.6 Commit per record versus batch

### Per-record commit

```text
Process one record
Commit
Repeat
```

This is simple but expensive.

### Batch commit

```text
Poll 100 records
Process 100 records
Commit next offsets
```

This is more efficient but may replay a larger batch after failure.

---

# 14. Consumer Group Coordinator

A broker acts as the coordinator for each consumer group.

The coordinator manages:

- Group membership
- Consumer heartbeats
- Join requests
- Leave requests
- Rebalances
- Offset commits
- Offset retrieval
- Group state

```text
Consumer C1 ----+
Consumer C2 ----+----> Group Coordinator Broker
Consumer C3 ----+
```

## 14.1 Heartbeats

Consumers send heartbeats to prove they are alive.

```text
Consumer ---- heartbeat ----> Coordinator
```

If heartbeats stop for too long, the coordinator considers the consumer dead.

## 14.2 Session timeout

The session timeout controls how long the coordinator waits before removing an unresponsive consumer.

Relevant setting:

```text
session.timeout.ms
```

## 14.3 Heartbeat interval

Consumers send heartbeats at intervals.

Relevant setting:

```text
heartbeat.interval.ms
```

The heartbeat interval must be comfortably lower than the session timeout.

## 14.4 Maximum poll interval

A consumer must continue calling poll within the configured limit.

```text
max.poll.interval.ms
```

If processing takes longer than this without polling, Kafka may remove the consumer from the group and rebalance.

---

# 15. Consumer Rebalancing

A rebalance redistributes partitions among consumers in the group.

A rebalance may occur when:

- A consumer joins.
- A consumer leaves.
- A consumer crashes.
- Heartbeats stop.
- Topic partition count changes.
- Subscription changes.
- The group coordinator changes.

## 15.1 Example

Before:

```text
C1 -> P0, P1
C2 -> P2, P3
```

A third consumer joins.

After rebalance:

```text
C1 -> P0, P1
C2 -> P2
C3 -> P3
```

## 15.2 Why rebalances are expensive

During a rebalance:

- Processing may pause.
- Consumers revoke partitions.
- New assignments are calculated.
- Consumers receive new partitions.
- Consumers resume from committed offsets.

Frequent rebalances reduce throughput and increase latency.

## 15.3 Rebalance failure scenario

```text
1. C1 processes P0 up to offset 99.
2. C1 does not commit offset 100.
3. Rebalance occurs.
4. C2 receives P0.
5. C2 starts from older committed offset 90.
6. Offsets 90-99 are processed again.
```

This is why consumers must be idempotent.

## 15.4 Eager rebalance

All consumers revoke all partitions before reassignment.

This can cause a complete processing pause.

## 15.5 Cooperative rebalance

Partitions are moved incrementally.

This reduces interruption and is often preferred in production.

## 15.6 Static membership

Static membership gives a consumer instance a stable identity.

Relevant setting:

```text
group.instance.id
```

It can reduce unnecessary rebalances during short restarts.

---

# 16. Replication

Replication creates multiple copies of a partition on different brokers.

Example:

```text
Replication factor = 3
```

Partition 0 may exist as:

```text
Broker 1 -> P0 leader
Broker 2 -> P0 follower
Broker 3 -> P0 follower
```

## 16.1 Why replication is required

Replication protects against:

- Broker failure
- Disk failure
- Server restart
- Network interruption
- Planned maintenance

Without replication:

```text
Replication factor = 1
Broker fails
Partition becomes unavailable
```

## 16.2 Replication factor

The replication factor is the number of copies of each partition.

```text
RF=1 -> one copy
RF=2 -> two copies
RF=3 -> three copies
```

Production clusters commonly use replication factor 3.

## 16.3 Example distribution

```text
              Broker 1      Broker 2      Broker 3
P0            Leader        Follower      Follower
P1            Follower      Leader        Follower
P2            Follower      Follower      Leader
```

This spreads leadership and storage across brokers.

## 16.4 Replication is partition-based

Kafka replicates partitions, not entire topics as one unit.

Each partition has its own:

- Leader
- Followers
- ISR
- Replication state

---

# 17. Leader and Follower Replicas

Each partition has one leader replica.

Clients normally read and write through the partition leader.

```text
Producer ----> P0 Leader on Broker 1
Consumer <---- P0 Leader on Broker 1
```

Follower replicas copy records from the leader.

```text
Broker 1: P0 Leader
    |
    +---- replication ----> Broker 2: P0 Follower
    |
    +---- replication ----> Broker 3: P0 Follower
```

## 17.1 Why one leader?

The leader establishes one authoritative order of writes.

Without a single leader, several brokers could accept conflicting writes.

## 17.2 Follower behavior

A follower:

- Fetches data from the leader.
- Appends copied records locally.
- Reports its progress.
- Can become leader if the current leader fails.

## 17.3 Leader failure

Before failure:

```text
Broker 1 -> P0 leader
Broker 2 -> P0 follower
Broker 3 -> P0 follower
```

Broker 1 fails.

Kafka elects an eligible replica:

```text
Broker 2 -> new P0 leader
Broker 3 -> P0 follower
```

Clients refresh metadata and communicate with the new leader.

---

# 18. ISR — In-Sync Replicas

ISR means **In-Sync Replicas**.

The ISR is the set of replicas sufficiently caught up with the leader.

Example:

```text
P0 replicas = [Broker 1, Broker 2, Broker 3]
P0 ISR      = [Broker 1, Broker 2, Broker 3]
```

If Broker 3 falls too far behind:

```text
P0 ISR = [Broker 1, Broker 2]
```

Broker 3 still has a replica, but it is no longer considered in sync.

## 18.1 Why ISR matters

ISR affects:

- Durability
- Leader election
- Producer acknowledgements
- Availability

## 18.2 min.insync.replicas

This setting defines the minimum number of in-sync replicas required for successful writes when the producer uses `acks=all`.

Example:

```text
replication.factor = 3
min.insync.replicas = 2
acks = all
```

Kafka requires at least two ISR members.

If only one in-sync replica remains, writes fail rather than risk weak durability.

## 18.3 Availability versus durability

Lower requirements improve availability but can reduce durability.

Higher requirements improve durability but can reject writes during failures.

Example:

```text
RF=3
min.insync.replicas=2
acks=all
```

This allows one broker failure while preserving strong write durability.

---

# 19. Producer Acknowledgements

The producer `acks` configuration controls when a write is considered successful.

## 19.1 acks=0

The producer does not wait for a broker acknowledgement.

```text
Producer ---- send ----> Broker
Producer immediately assumes success
```

Advantages:

- Lowest latency
- Highest throughput

Risks:

- Records may be lost without the producer knowing
- Errors are difficult to detect

## 19.2 acks=1

The partition leader acknowledges after writing locally.

```text
Producer -> Leader
Leader writes locally
Leader -> success
```

Risk:

```text
1. Leader acknowledges.
2. Followers have not copied the record.
3. Leader fails.
4. A follower without the record becomes leader.
5. Record is lost.
```

## 19.3 acks=all

The leader waits until all required in-sync replicas acknowledge.

```text
Producer -> Leader
Leader -> Followers
Required ISR replicas acknowledge
Leader -> Producer success
```

This gives stronger durability.

Recommended production combination:

```text
acks=all
enable.idempotence=true
replication.factor=3
min.insync.replicas=2
```

---

# 20. Delivery Guarantees

Kafka applications commonly implement one of three delivery models.

## 20.1 At-most-once

A record is processed zero or one time.

Pattern:

```text
Commit first
Process second
```

If processing fails, the event may be lost.

## 20.2 At-least-once

A record is processed one or more times.

Pattern:

```text
Process first
Commit second
```

If the process crashes after processing but before committing, the event is processed again.

This is the most common pattern.

Consumers should therefore be idempotent.

## 20.3 Exactly-once

Exactly-once means that the intended effect occurs once despite retries and failures.

Kafka supports exactly-once semantics for certain Kafka-to-Kafka transactional workflows.

However, writing to an external database introduces a distributed transaction problem.

Example:

```text
1. Consume Kafka record.
2. Insert into PostgreSQL.
3. Crash before offset commit.
4. Record is consumed again.
5. Duplicate insert may occur.
```

Typical solutions include:

- Idempotency keys
- Unique constraints
- Transactional outbox
- Inbox pattern
- Kafka transactions for Kafka-to-Kafka processing
- Deduplication tables

## 20.4 Idempotent consumer

An idempotent consumer can safely process the same event multiple times.

Example:

```sql
INSERT INTO processed_events(event_id)
VALUES ('event-1001')
ON CONFLICT DO NOTHING;
```

If the event was already processed, the second execution has no effect.

---

# 21. Ordering

Kafka guarantees ordering only within one partition.

```text
P0:
Offset 10 -> OrderCreated
Offset 11 -> PaymentCompleted
Offset 12 -> OrderShipped
```

A consumer reading P0 observes this order.

## 21.1 No global ordering across partitions

```text
P0 -> A, C, E
P1 -> B, D, F
```

Kafka does not guarantee whether B was observed before or after C across partitions.

## 21.2 Key-based ordering

Use the same key for events that require relative order.

Example:

```text
Key = order-1001
```

Events:

```text
OrderCreated
PaymentCompleted
OrderShipped
```

They are routed to the same partition and preserve partition order.

## 21.3 Ordering and retries

Producer retries can affect ordering unless idempotence and appropriate in-flight settings are used.

Modern Kafka clients normally enable safer idempotent behavior by default or through configuration, but application configuration should still be verified.

---

# 22. Retention

Kafka retains records based on policy.

Reading a record does not delete it.

## 22.1 Time-based retention

Example:

```text
retention.ms = 604800000
```

This is seven days.

Records older than the retention window become eligible for deletion.

## 22.2 Size-based retention

Example:

```text
retention.bytes = 10737418240
```

When a partition log exceeds the configured size, old segments become eligible for deletion.

## 22.3 Retention is segment-based

Kafka does not usually delete one old record at a time.

Kafka deletes old log segments.

Therefore, actual deletion timing is not exact at the individual record level.

## 22.4 Consumer slower than retention

Suppose:

```text
Retention = 1 day
Consumer is offline = 3 days
```

The consumer's committed offset may refer to data that has already been deleted.

The consumer then uses `auto.offset.reset` behavior.

Possible options:

```text
earliest
latest
none
```

---

# 23. Log Compaction

Log compaction keeps the latest value for each key.

Example stream:

```text
Offset 0: key=A, value=10
Offset 1: key=B, value=20
Offset 2: key=A, value=15
Offset 3: key=A, value=30
```

After compaction, Kafka eventually retains the latest value:

```text
key=A, value=30
key=B, value=20
```

## 23.1 Compaction is not immediate

Compaction runs asynchronously.

Old values may remain for some time.

## 23.2 Tombstone records

A record with a key and null value acts as a deletion marker.

```text
key=A
value=null
```

This is called a tombstone.

After compaction and retention conditions, Kafka can remove the key's older values.

## 23.3 Common use cases

Compaction is useful for:

- Latest customer state
- Configuration state
- Cache rebuilding
- Change data capture
- Entity snapshots
- Kafka Streams state changelogs

---

# 24. Kafka Storage Internals

Kafka stores each partition as a log directory.

Conceptually:

```text
orders.created.v1-0/
orders.created.v1-1/
orders.created.v1-2/
```

Each partition log is divided into segments.

```text
00000000000000000000.log
00000000000000000000.index
00000000000000000000.timeindex

00000000000001000000.log
00000000000001000000.index
00000000000001000000.timeindex
```

## 24.1 Log segment

A segment is a portion of a partition log.

Kafka writes to the active segment.

When the segment reaches a configured size or age, Kafka rolls to a new segment.

## 24.2 Offset index

The offset index maps logical offsets to positions in the log file.

Conceptually:

```text
Offset 1000 -> byte position 4201
Offset 1050 -> byte position 8700
```

## 24.3 Time index

The time index helps Kafka locate offsets by timestamp.

## 24.4 Sequential I/O

Kafka is efficient because it primarily uses sequential disk access, batching, page cache, and zero-copy-related optimizations.

Sequential append workloads are much faster than random disk updates.

## 24.5 Record batches

Kafka stores records in batches.

Compression is often applied at the batch level.

Common compression types include:

```text
none
gzip
snappy
lz4
zstd
```

Compression reduces:

- Network usage
- Disk usage

It may increase CPU usage.

---

# 25. KRaft

KRaft stands for **Kafka Raft Metadata mode**.

KRaft replaces ZooKeeper for Kafka metadata management.

Older architecture:

```text
Kafka Brokers <----> ZooKeeper Ensemble
```

Modern architecture:

```text
Kafka Brokers <----> KRaft Controller Quorum
```

## 25.1 What metadata means

Kafka cluster metadata includes:

- Broker registrations
- Topic definitions
- Partition assignments
- Partition leaders
- Replica assignments
- ISR changes
- Cluster configuration
- Security-related metadata
- Controller state

## 25.2 Controller role

The active KRaft controller manages cluster metadata and administrative state transitions.

The controller does not normally carry ordinary producer and consumer data traffic.

## 25.3 Raft consensus

KRaft uses a Raft-style quorum to maintain a consistent metadata log.

Controllers agree on the order of metadata changes.

Example:

```text
Create topic orders.created.v1
Assign P0 replicas to brokers 1,2,3
Elect Broker 1 as P0 leader
```

These changes are written to the metadata log.

## 25.4 KRaft benefits

KRaft provides:

- No ZooKeeper dependency
- Simpler deployment
- Unified Kafka metadata management
- Faster metadata propagation
- Better scalability
- Cleaner failover model
- Fewer operational components

---

# 26. KRaft Controller Quorum

A production KRaft cluster usually uses an odd number of controllers.

Common choices:

```text
3 controllers
5 controllers
```

## 26.1 Why odd numbers?

Raft requires a majority.

For three controllers:

```text
Majority = 2
```

The cluster can tolerate one controller failure.

For five controllers:

```text
Majority = 3
```

The cluster can tolerate two controller failures.

## 26.2 Controller leader and followers

```text
Controller 1 -> active leader
Controller 2 -> follower
Controller 3 -> follower
```

If Controller 1 fails:

```text
Controller 2 or Controller 3 becomes leader
```

## 26.3 Combined mode

Your development configuration uses:

```yaml
KAFKA_PROCESS_ROLES: broker,controller
```

This means one process performs both roles.

This is convenient for local development.

## 26.4 Dedicated mode

Production architecture often separates roles:

```text
Controller nodes:
controller-1
controller-2
controller-3

Broker nodes:
broker-1
broker-2
broker-3
broker-4
```

Benefits:

- Better fault isolation
- More predictable performance
- Cleaner scaling
- Reduced controller workload interference

## 26.5 Controller quorum voters

Example:

```text
1@controller-1:9093
2@controller-2:9093
3@controller-3:9093
```

Each entry includes:

```text
node.id@host:controller-port
```

---

# 27. End-to-End Message Flow

Consider this event:

```json
{
  "orderId": "ORD-1001",
  "amount": 2500
}
```

Topic:

```text
orders.created.v1
```

Key:

```text
ORD-1001
```

## 27.1 Step 1 — Application creates the event

```text
Order API creates OrderCreated event
```

## 27.2 Step 2 — Producer serializes it

```text
C# object -> JSON -> bytes
```

## 27.3 Step 3 — Producer selects partition

Suppose:

```text
hash(ORD-1001) % 3 = 2
```

The record goes to partition 2.

## 27.4 Step 4 — Producer discovers leader

Metadata says:

```text
Partition 2 leader = Broker 3
```

## 27.5 Step 5 — Producer sends batch

```text
Producer -> Broker 3
```

## 27.6 Step 6 — Leader appends record

Suppose the next offset is 145.

```text
Topic: orders.created.v1
Partition: 2
Offset: 145
```

## 27.7 Step 7 — Followers replicate

```text
Broker 3 leader
   |
   +--> Broker 1 follower
   +--> Broker 2 follower
```

## 27.8 Step 8 — Producer receives acknowledgement

With `acks=all`, Kafka responds after the required ISR replicas acknowledge.

## 27.9 Step 9 — Consumer polls

The consumer assigned to partition 2 sends:

```text
Fetch from offset 145
```

Kafka returns the record.

## 27.10 Step 10 — Consumer deserializes

```text
bytes -> JSON -> OrderCreated object
```

## 27.11 Step 11 — Consumer processes

Example:

```text
Insert order into consumer database
```

## 27.12 Step 12 — Consumer commits

After successful processing:

```text
Commit offset 146
```

This means offset 146 should be read next.

---

# 28. Your Docker Compose Configuration

Your current Kafka service uses:

```yaml
KAFKA_NODE_ID: 1
KAFKA_PROCESS_ROLES: broker,controller
```

This is a single-node combined KRaft setup.

## 28.1 Listeners

Your configuration contains:

```yaml
KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093,PLAINTEXT_HOST://0.0.0.0:29092
```

Meaning:

```text
9092  -> internal Docker clients
9093  -> KRaft controller traffic
29092 -> host machine clients
```

## 28.2 Advertised listeners

```yaml
KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092,PLAINTEXT_HOST://localhost:29092
```

Docker containers connect using:

```text
kafka:9092
```

Applications running on your Mac connect using:

```text
localhost:29092
```

## 28.3 Topic creation

Your topic is:

```text
orders.created.v1
```

Configuration:

```text
Partitions = 3
Replication factor = 1
```

Because your cluster has only one broker, replication factor must be 1.

## 28.4 Consumer group

Your consumer uses:

```text
order-processor-v1
```

Kafka tracks offsets separately for:

```text
order-processor-v1 / orders.created.v1 / partition 0
order-processor-v1 / orders.created.v1 / partition 1
order-processor-v1 / orders.created.v1 / partition 2
```

---

# 29. Important Kafka Commands

## List topics

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server localhost:9092 \
  --list
```

## Describe topic

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server localhost:9092 \
  --describe \
  --topic orders.created.v1
```

## Produce records

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-console-producer.sh \
  --bootstrap-server localhost:9092 \
  --topic orders.created.v1
```

## Consume from beginning

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server localhost:9092 \
  --topic orders.created.v1 \
  --from-beginning
```

## Consume using a group

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server localhost:9092 \
  --topic orders.created.v1 \
  --group order-processor-v1
```

## List consumer groups

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 \
  --list
```

## Describe group and lag

```bash
docker exec -it pubsub-kafka \
  /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 \
  --describe \
  --group order-processor-v1
```

Typical output:

```text
GROUP               TOPIC              PARTITION CURRENT-OFFSET LOG-END-OFFSET LAG
order-processor-v1  orders.created.v1  0         100            110            10
order-processor-v1  orders.created.v1  1         75             75             0
order-processor-v1  orders.created.v1  2         80             95             15
```

---

# 30. Common Misunderstandings

## Misunderstanding 1

> Kafka deletes a message after a consumer reads it.

Incorrect.

Kafka retains records according to retention or compaction policy.

## Misunderstanding 2

> An offset is a message ID.

Incorrect.

An offset is a position within one partition.

## Misunderstanding 3

> A topic is one ordered queue.

Incorrect.

A topic contains one or more partitions, and ordering is guaranteed only within each partition.

## Misunderstanding 4

> More consumers always improve performance.

Incorrect.

Consumers beyond the number of partitions remain idle in the same group.

## Misunderstanding 5

> Replication factor 3 means three leaders.

Incorrect.

Each partition has one leader and two follower replicas.

## Misunderstanding 6

> KRaft stores normal event records.

Incorrect.

KRaft manages Kafka metadata. Brokers store ordinary topic records.

## Misunderstanding 7

> Committed offset 10 means offset 10 was processed.

Usually incorrect.

It normally means offset 10 is the next record to read.

## Misunderstanding 8

> Exactly-once means duplicate records can never exist anywhere.

Incorrect.

Exactly-once guarantees depend on the processing boundary and system design.

External databases often require idempotency or transactional patterns.

---

# 31. Production Recommendations

## Topic design

- Use meaningful topic names.
- Version event contracts.
- Avoid mixing unrelated event types.
- Choose partition counts carefully.
- Plan for expected throughput and parallelism.

## Replication

Typical production setup:

```text
replication.factor=3
min.insync.replicas=2
acks=all
```

## Producer

- Enable idempotence.
- Use retries.
- Use batching.
- Use compression when appropriate.
- Monitor produce errors and latency.

## Consumer

- Disable unsafe auto commits for critical workflows.
- Commit after successful processing.
- Implement idempotency.
- Handle poison messages.
- Use dead-letter topics where appropriate.
- Monitor lag.
- Handle rebalances correctly.
- Shut down gracefully.

## KRaft

- Use three or five controller nodes.
- Prefer dedicated controllers for larger production environments.
- Use an odd-sized quorum.
- Monitor controller quorum health.
- Back up and protect configuration and security material.

## Observability

Monitor:

- Consumer lag
- Under-replicated partitions
- Offline partitions
- ISR shrink events
- Broker disk usage
- Request latency
- Produce error rate
- Fetch error rate
- Controller leadership
- Network throughput
- JVM memory and garbage collection

---

# 32. Glossary

| Term | Meaning |
|---|---|
| Broker | Kafka server that stores and serves records |
| Cluster | Collection of Kafka brokers and controllers |
| Topic | Named logical event stream |
| Partition | Ordered append-only log inside a topic |
| Record | Kafka message or event |
| Key | Optional value used for partitioning and identity |
| Value | Main event payload |
| Header | Metadata attached to a record |
| Offset | Record position inside a partition |
| Producer | Application that publishes records |
| Consumer | Application that reads records |
| Consumer group | Consumers that share partition processing |
| Group coordinator | Broker that manages a consumer group |
| Lag | Difference between log end and committed position |
| Replica | Copy of a partition |
| Leader | Replica that serves normal reads and writes |
| Follower | Replica that copies from the leader |
| ISR | Replicas sufficiently synchronized with the leader |
| Replication factor | Number of copies of each partition |
| Retention | Policy controlling how long records remain |
| Compaction | Policy retaining latest value per key |
| KRaft | Kafka metadata quorum based on Raft |
| Controller | Node that manages cluster metadata |
| Rebalance | Redistribution of partitions among consumers |
| Idempotence | Ability to repeat an operation without duplicate effect |
| Tombstone | Null-valued record used to delete a key in compacted topics |

---

# Final Mental Model

Think of Kafka using this hierarchy:

```text
Kafka Cluster
    |
    +---- Brokers
            |
            +---- Topics
                    |
                    +---- Partitions
                            |
                            +---- Ordered records
                                    |
                                    +---- Offsets
```

Consumer progress is tracked as:

```text
Consumer Group
    +
Topic
    +
Partition
    =
Committed Offset
```

Replication works as:

```text
Partition
    |
    +---- One leader
    |
    +---- Zero or more followers
    |
    +---- ISR tracks healthy synchronized replicas
```

KRaft works as:

```text
Controller quorum
    |
    +---- Stores and agrees on Kafka metadata
    |
    +---- Elects an active controller
    |
    +---- Coordinates topic, partition, broker, and leadership state
```

The most important Kafka principle is:

> A topic is divided into ordered partitions, brokers store and replicate those partitions, producers append records, consumers read by offset, and consumer groups independently track their progress.
