$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot

$CaCert = Join-Path $Root "certs\ca\ca.crt"
$ClientCert = Join-Path $Root "certs\client\client.crt"
$ClientKey = Join-Path $Root "certs\client\client.key"

Write-Host "1. Verifying certificate chains..."

$certificateNames = @(
    "nats1",
    "nats2",
    "nats3",
    "client"
)

foreach ($name in $certificateNames) {
    $certificate = Join-Path $Root "certs\$name\$name.crt"

    & openssl verify `
        -CAfile $CaCert `
        $certificate

    if ($LASTEXITCODE -ne 0) {
        throw "Certificate verification failed for $name."
    }
}

Write-Host ""
Write-Host "2. Checking mTLS handshake through node 1..."

$handshakeOutput = "" | & openssl s_client `
    -connect "localhost:4222" `
    -servername "localhost" `
    -CAfile $CaCert `
    -cert $ClientCert `
    -key $ClientKey `
    -verify_return_error `
    2>$null

$handshakeOutput |
    Select-String -Pattern "Protocol|Cipher|Verification|Verify return code" |
    ForEach-Object {
        Write-Host $_.Line
    }

if ($LASTEXITCODE -ne 0) {
    Write-Warning "The OpenSSL mTLS handshake command returned a non-zero exit code."
}

Write-Host ""
Write-Host "3. Checking cluster routes from monitoring endpoint..."

try {
    $routeResponse = Invoke-RestMethod `
        -Uri "http://localhost:8222/routez" `
        -Method Get

    $routeResponse | ConvertTo-Json -Depth 20
}
catch {
    Write-Error "Could not retrieve the NATS route monitoring endpoint: $($_.Exception.Message)"
    exit 1
}