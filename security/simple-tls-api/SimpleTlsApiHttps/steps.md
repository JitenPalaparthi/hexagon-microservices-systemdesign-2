Generate the CA private key

openssl genrsa -out ca.key 4096

inspect it

openssl rsa -in ca.key -check -noout

chmod 600 ca.key


Create the CA certificate

openssl req \
  -x509 \
  -new \
  -sha256 \
  -days 3650 \
  -key ca.key \
  -out ca.crt \
  -subj "/C=IN/ST=Telangana/L=Hyderabad/O=Local Demo CA/OU=Development/CN=JP Local Root CA"

Inspect the CA certificate:

openssl x509 -in ca.crt -text -noout

display its identity

openssl x509 \
  -in ca.crt \
  -noout \
  -subject \
  -issuer \
  -dates \
  -fingerprint \
  -sha256


create server private key

openssl genrsa -out localhost.key 2048
chmod 600 localhost.key

inspect it

openssl rsa -in localhost.key -check -noout

Create an OpenSSL request configuration

cat > localhost.cnf <<'EOF'
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = distinguished_name
req_extensions = request_extensions

[distinguished_name]
C = IN
ST = Telangana
L = Hyderabad
O = Simple TLS API
OU = Development
CN = localhost

[request_extensions]
subjectAltName = @alternative_names

[alternative_names]
DNS.1 = localhost
DNS.2 = host.docker.internal
IP.1 = 127.0.0.1
IP.2 = ::1
EOF


5. Create a Certificate Signing Request

openssl req \
  -new \
  -key localhost.key \
  -out localhost.csr \
  -config localhost.cnf

inspect
openssl req -in localhost.csr -text -noout

verify signature 
openssl req -in localhost.csr -verify -noout


 Define the server certificate extensions

cat > localhost.ext <<'EOF'
authorityKeyIdentifier = keyid,issuer
basicConstraints = critical,CA:FALSE
keyUsage = critical,digitalSignature,keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alternative_names

[alternative_names]
DNS.1 = localhost
DNS.2 = host.docker.internal
IP.1 = 127.0.0.1
IP.2 = ::1
EOF

Sign the server certificate using your CA

openssl x509 \
  -req \
  -in localhost.csr \
  -CA ca.crt \
  -CAkey ca.key \
  -CAcreateserial \
  -out localhost.crt \
  -days 825 \
  -sha256 \
  -extfile localhost.ext
  
inspect

openssl x509 -in localhost.crt -text -noout

check important fields

openssl x509 \
  -in localhost.crt \
  -noout \
  -subject \
  -issuer \
  -dates \
  -serial

Verify that the CA signed it correctly:

openssl verify -CAfile ca.crt localhost.crt


Verify the Subject Alternative Names

openssl x509 -in localhost.crt -text -noout

Export the certificate as a PFX

openssl pkcs12 \
  -export \
  -out localhost.pfx \
  -inkey localhost.key \
  -in localhost.crt \
  -certfile ca.crt \
  -name "SimpleTlsApi localhost"

give this password ChangeThisDemoPassword123!

chmod 600 localhost.pfx

inspect pfx

openssl pkcs12 \
  -in localhost.pfx \
  -info \
  -noout

Configure Kestrel using appsettings.json

replace appsettings.json

{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5080"
      },
      "Https": {
        "Url": "https://localhost:7243",
        "Protocols": "Http1AndHttp2",
        "Certificate": {
          "Path": "certificates/localhost.pfx",
          "Password": "ChangeThisDemoPassword123!"
        }
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "AllowedHosts": "*"
}

export Kestrel__Endpoints__Https__Certificate__Password='ChangeThisDemoPassword123!'

dotnet run --no-launch-profile

curl \
  --cacert certificates/ca.crt \
  https://localhost:7243/hello

15. Trust your private CA on macOS

sudo security add-trusted-cert \
  -d \
  -r trustRoot \
  -k /Library/Keychains/System.keychain \
  "$(pwd)/certificates/ca.crt"

curl -v https://localhost:7243/hello

