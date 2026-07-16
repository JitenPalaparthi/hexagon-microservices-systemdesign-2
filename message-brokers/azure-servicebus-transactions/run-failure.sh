#!/usr/bin/env bash
set -euo pipefail

docker compose up -d sql servicebus-emulator
docker compose --profile tools build demo
docker compose --profile tools run --rm demo wait
docker compose --profile tools run --rm demo reset
docker compose --profile tools run --rm demo seed
docker compose --profile tools run --rm demo failure
