# API Reference

## POST `/api/orders`

Creates an order and atomically inserts an `OrderCreatedEvent` into the transactional outbox.

### Request

```json
{
  "customerName": "Jiten",
  "product": "Mechanical Keyboard",
  "quantity": 2
}
```

### Validation

- `customerName`: required, 2–150 characters.
- `product`: required, 2–200 characters.
- `quantity`: 1–10,000.

### Responses

- `201 Created`: order and outbox event were committed.
- `400 Bad Request`: validation failed.
- `500 Internal Server Error`: unexpected infrastructure/application failure.

## GET `/api/orders/{id}`

Returns the order and whether its outbox event has been published.

- `200 OK`: order found.
- `404 Not Found`: unknown order ID.

## GET `/health`

Returns API process liveness.

## GET `/openapi/v1.json`

Returns the generated OpenAPI document.
