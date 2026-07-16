CREATE DATABASE consumerdb;

\connect ordersdb

CREATE TABLE IF NOT EXISTS orders (
    id UUID PRIMARY KEY,
    customer_name VARCHAR(150) NOT NULL,
    product VARCHAR(200) NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS outbox_messages (
    event_id UUID PRIMARY KEY,
    aggregate_id UUID NOT NULL REFERENCES orders(id),
    event_type VARCHAR(200) NOT NULL,
    payload JSONB NOT NULL,
    occurred_at_utc TIMESTAMPTZ NOT NULL,
    published_at_utc TIMESTAMPTZ NULL,
    kafka_partition INTEGER NULL,
    kafka_offset BIGINT NULL
);

CREATE INDEX IF NOT EXISTS ix_outbox_unpublished
    ON outbox_messages (occurred_at_utc)
    WHERE published_at_utc IS NULL;

\connect consumerdb

CREATE TABLE IF NOT EXISTS processed_orders (
    event_id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    customer_name VARCHAR(150) NOT NULL,
    product VARCHAR(200) NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    order_created_at_utc TIMESTAMPTZ NOT NULL,
    processed_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_processed_orders_order_id
    ON processed_orders (order_id);
