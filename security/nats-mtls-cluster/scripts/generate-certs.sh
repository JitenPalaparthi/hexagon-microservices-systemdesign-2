#!/usr/bin/env sh
set -eu

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
CERTS="$ROOT/certs"

mkdir -p "$CERTS/ca" "$CERTS/nats1" "$CERTS/nats2" "$CERTS/nats3" "$CERTS/client"

echo "Generating local development CA..."
openssl genrsa -out "$CERTS/ca/ca.key" 4096
openssl req -x509 -new -sha256 \
  -key "$CERTS/ca/ca.key" \
  -days 3650 \
  -subj "/C=IN/ST=Telangana/L=Hyderabad/O=NATS Demo/CN=NATS Demo Root CA" \
  -out "$CERTS/ca/ca.crt"

generate_cert() {
  NAME="$1"
  TYPE="$2"
  DIR="$CERTS/$NAME"

  openssl genrsa -out "$DIR/$NAME.key" 2048
  chmod 600 "$DIR/$NAME.key"

  if [ "$TYPE" = "server" ]; then
    cat > "$DIR/$NAME.ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth,clientAuth
subjectAltName=DNS:$NAME,DNS:localhost,IP:127.0.0.1
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid,issuer
EOF
  else
    cat > "$DIR/$NAME.ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=clientAuth
subjectAltName=DNS:$NAME
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid,issuer
EOF
  fi

  openssl req -new -sha256 \
    -key "$DIR/$NAME.key" \
    -subj "/C=IN/ST=Telangana/L=Hyderabad/O=NATS Demo/OU=$TYPE/CN=$NAME" \
    -out "$DIR/$NAME.csr"

  openssl x509 -req -sha256 \
    -in "$DIR/$NAME.csr" \
    -CA "$CERTS/ca/ca.crt" \
    -CAkey "$CERTS/ca/ca.key" \
    -CAcreateserial \
    -days 825 \
    -extfile "$DIR/$NAME.ext" \
    -out "$DIR/$NAME.crt"

  rm -f "$DIR/$NAME.csr" "$DIR/$NAME.ext"
}

generate_cert nats1 server
generate_cert nats2 server
generate_cert nats3 server
generate_cert client client

chmod 644 "$CERTS/ca/ca.crt" \
  "$CERTS/nats1/nats1.crt" \
  "$CERTS/nats2/nats2.crt" \
  "$CERTS/nats3/nats3.crt" \
  "$CERTS/client/client.crt"

echo
echo "Certificates generated."
echo "CA private key: $CERTS/ca/ca.key"
echo "IMPORTANT: ca.key is intentionally not mounted into any container."
