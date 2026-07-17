#!/usr/bin/env sh
set -eu

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

echo "1. Verifying certificate chains..."
for name in nats1 nats2 nats3 client; do
  openssl verify \
    -CAfile "$ROOT/certs/ca/ca.crt" \
    "$ROOT/certs/$name/$name.crt"
done

echo
echo "2. Checking mTLS handshake through node 1..."
openssl s_client \
  -connect localhost:4222 \
  -servername localhost \
  -CAfile "$ROOT/certs/ca/ca.crt" \
  -cert "$ROOT/certs/client/client.crt" \
  -key "$ROOT/certs/client/client.key" \
  -verify_return_error \
  </dev/null 2>/dev/null | grep -E "Protocol|Cipher|Verification|Verify return code" || true

echo
echo "3. Checking cluster routes from monitoring endpoint..."
curl -fsS http://localhost:8222/routez
echo
