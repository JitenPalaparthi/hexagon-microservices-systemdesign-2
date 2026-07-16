# .NET 10 + Nginx Load Balancer

This example runs the same .NET 10 backend application as two independent containers and places Nginx in front of them.

## Architecture

```text
Client
  |
  v
Nginx :8080
  |-- /api/shared/*   --> round-robin --> backend1:8080
  |                                  --> backend2:8080
  |-- /api/backend1/* --> backend1:8080
  `-- /api/backend2/* --> backend2:8080
```

## Start

```bash
docker compose down -v --remove-orphans
docker compose up --build -d
```

Check status:

```bash
docker compose ps
```

## Test direct routing

Backend 1:

```bash
curl http://localhost:8080/api/backend1/info
```

Backend 2:

```bash
curl http://localhost:8080/api/backend2/info
```

## Test round-robin load balancing

```bash
for i in {1..10}; do
  curl -s http://localhost:8080/api/shared/info
  echo
done
```

Or use:

```bash
./test-loadbalance.sh 12
```

Expected instance names alternate approximately like this:

```text
backend-1
backend-2
backend-1
backend-2
```

Round-robin is deterministic for new upstream requests, but connection reuse and runtime timing can make the visible sequence differ slightly.

## Routing table

| Public route | Backend target | Purpose |
|---|---|---|
| `/api/shared/info` | backend1 or backend2 | Load-balanced request |
| `/api/shared/work?delayMs=500` | backend1 or backend2 | Simulated work |
| `/api/shared/echo` | backend1 or backend2 | POST payload test |
| `/api/backend1/info` | backend1 only | Direct routing |
| `/api/backend2/info` | backend2 only | Direct routing |
| `/health` | Nginx | Load-balancer health |

## POST through the load balancer

```bash
curl -X POST http://localhost:8080/api/shared/echo \
  -H 'Content-Type: application/json' \
  -d '{"message":"hello through nginx"}'
```

## Watch logs

```bash
docker compose logs -f nginx backend1 backend2
```

Nginx logs include the selected upstream address:

```text
upstream=172.x.x.x:8080
```

## Stop one backend and test failover

```bash
docker compose stop backend1
```

Send several requests:

```bash
for i in {1..5}; do
  curl -s http://localhost:8080/api/shared/info
  echo
done
```

Nginx routes requests to the surviving backend after detecting failures.

Restart backend 1:

```bash
docker compose start backend1
```

## Change the load-balancing algorithm

The current configuration uses Nginx's default round-robin algorithm.

For least connections, add this inside `upstream dotnet_backends`:

```nginx
least_conn;
```

For client-IP affinity, add:

```nginx
ip_hash;
```

Use only one balancing directive at a time.

## Important Nginx path behavior

This route:

```nginx
location /api/shared/ {
    proxy_pass http://dotnet_backends/api/;
}
```

rewrites:

```text
/api/shared/info -> /api/info
```

before forwarding the request to the selected .NET backend.
