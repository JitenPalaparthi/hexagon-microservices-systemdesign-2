# Three-node NATS cluster with mTLS

This project runs:

- `nats1`, `nats2`, and `nats3`
- mTLS on client port `4222`
- mutually authenticated TLS on cluster route port `6222`
- a .NET 10 subscriber using `NATS.Net`
- a one-shot .NET 10 publisher

## Certificate ownership

| Material | Stored where | Mounted at runtime? |
|---|---|---|
| `ca.key` | `certs/ca` on the administrative machine | No |
| `ca.crt` | `certs/ca` | Yes, as trust anchor |
| Node private key | That node's certificate directory | Only into that node |
| Client private key | `certs/client` | Only into the client |
| Node/client certificate | Matching directory | Yes |

This is a development example. Store the CA private key offline or in a PKI/secrets system for production.

## 1. Generate the CA and certificates

```bash
chmod +x scripts/*.sh
./scripts/generate-certs.sh
```

The node certificates contain:

```text
serverAuth, clientAuth
```

Both usages are required because a cluster node accepts incoming route connections and also initiates outgoing route connections.

The client certificate contains:

```text
clientAuth
```

## 2. Start the cluster and subscriber

```bash
docker compose up --build -d
docker compose ps
docker compose logs -f subscriber
```

Host mappings:

| Node | Client TLS | Monitoring HTTP |
|---|---:|---:|
| nats1 | localhost:4222 | localhost:8222 |
| nats2 | localhost:4223 | localhost:8223 |
| nats3 | localhost:4224 | localhost:8224 |

Monitoring is intentionally plain HTTP and exposed only for local demonstration. Restrict or secure it in production.

## 3. Publish from the .NET client

Run a one-shot publisher in the Compose network:

```bash
docker compose run --rm \
  -e MODE=publish \
  subscriber \
  "Order-1001 created"
```

The subscriber logs should show the message:

```bash
docker compose logs --tail=50 subscriber
```

## 4. Run the .NET client directly from the host

Subscriber:

```bash
cd client

MODE=subscribe \
NATS_URL="tls://localhost:4222,tls://localhost:4223,tls://localhost:4224" \
NATS_CA_FILE="../certs/ca/ca.crt" \
NATS_CERT_FILE="../certs/client/client.crt" \
NATS_KEY_FILE="../certs/client/client.key" \
dotnet run
```

Publisher in another terminal:

```bash
cd client

MODE=publish \
NATS_URL="tls://localhost:4222,tls://localhost:4223,tls://localhost:4224" \
NATS_CA_FILE="../certs/ca/ca.crt" \
NATS_CERT_FILE="../certs/client/client.crt" \
NATS_KEY_FILE="../certs/client/client.key" \
dotnet run -- "Order-1002 created from host"
```

## 5. Verify TLS and cluster formation

```bash
./scripts/verify.sh
```

Inspect routes:

```bash
curl -s http://localhost:8222/routez | jq
```

A healthy three-node full mesh normally shows two active routes from each node.

Inspect server state:

```bash
curl -s http://localhost:8222/varz | jq
curl -s http://localhost:8223/varz | jq
curl -s http://localhost:8224/varz | jq
```

## 6. Prove that a certificate is mandatory

This connection does not supply a client certificate and must fail:

```bash
openssl s_client \
  -connect localhost:4222 \
  -servername localhost \
  -CAfile certs/ca/ca.crt
```

This connection supplies a valid client identity and succeeds:

```bash
openssl s_client \
  -connect localhost:4222 \
  -servername localhost \
  -CAfile certs/ca/ca.crt \
  -cert certs/client/client.crt \
  -key certs/client/client.key
```

## 7. Test failover

Keep the subscriber running, then stop node 1:

```bash
docker compose stop nats1
```

Publish again:

```bash
docker compose run --rm \
  -e MODE=publish \
  subscriber \
  "Message while nats1 is unavailable"
```

The clients know all three seed URLs and reconnect to another node.

Restart node 1:

```bash
docker compose start nats1
```

## 8. Stop and clean up

```bash
docker compose down
```

To remove generated certificates:

```bash
rm -rf certs
mkdir -p certs/{ca,nats1,nats2,nats3,client}
```

## Security notes

1. The CA is not a running Docker service.
2. Never mount `ca.key` into NATS or client containers.
3. Never share one private key across all nodes.
4. Verify SAN names; do not use `InsecureSkipVerify`.
5. Use separate issuing CAs or intermediates for node and application identities in larger production environments.
6. Add NATS authorization—accounts/JWT/NKeys or certificate mapping—when different clients need different subject permissions. mTLS authenticates the certificate holder, but `verify: true` alone does not create fine-grained publish/subscribe authorization.
