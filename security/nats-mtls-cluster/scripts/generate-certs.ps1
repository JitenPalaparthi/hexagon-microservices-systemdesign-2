$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Certs = Join-Path $Root "certs"

$folders = @(
    "ca",
    "nats1",
    "nats2",
    "nats3",
    "client"
)

foreach ($folder in $folders) {
    New-Item -ItemType Directory `
        -Force `
        -Path (Join-Path $Certs $folder) | Out-Null
}

Write-Host ""
Write-Host "Generating local development CA..."

openssl genrsa `
    -out "$Certs\ca\ca.key" `
    4096

openssl req `
    -x509 `
    -new `
    -sha256 `
    -key "$Certs\ca\ca.key" `
    -days 3650 `
    -subj "/C=IN/ST=Telangana/L=Hyderabad/O=NATS Demo/CN=NATS Demo Root CA" `
    -out "$Certs\ca\ca.crt"

function Generate-Certificate {

    param(
        [string]$Name,
        [string]$Type
    )

    $Dir = Join-Path $Certs $Name

    openssl genrsa `
        -out "$Dir\$Name.key" `
        2048

    if ($Type -eq "server") {

@"
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth,clientAuth
subjectAltName=DNS:$Name,DNS:localhost,IP:127.0.0.1
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid,issuer
"@ | Set-Content "$Dir\$Name.ext"

    }
    else {

@"
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=clientAuth
subjectAltName=DNS:$Name
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid,issuer
"@ | Set-Content "$Dir\$Name.ext"

    }

    openssl req `
        -new `
        -sha256 `
        -key "$Dir\$Name.key" `
        -subj "/C=IN/ST=Telangana/L=Hyderabad/O=NATS Demo/OU=$Type/CN=$Name" `
        -out "$Dir\$Name.csr"

    openssl x509 `
        -req `
        -sha256 `
        -in "$Dir\$Name.csr" `
        -CA "$Certs\ca\ca.crt" `
        -CAkey "$Certs\ca\ca.key" `
        -CAcreateserial `
        -days 825 `
        -extfile "$Dir\$Name.ext" `
        -out "$Dir\$Name.crt"

    Remove-Item "$Dir\$Name.csr"
    Remove-Item "$Dir\$Name.ext"
}

Generate-Certificate "nats1" "server"
Generate-Certificate "nats2" "server"
Generate-Certificate "nats3" "server"
Generate-Certificate "client" "client"

Write-Host ""
Write-Host "Certificates generated successfully."
Write-Host ""
Write-Host "CA Private Key : $Certs\ca\ca.key"
Write-Host "CA Certificate : $Certs\ca\ca.crt"
Write-Host ""
Write-Host "IMPORTANT: Do NOT mount ca.key into any Docker container."