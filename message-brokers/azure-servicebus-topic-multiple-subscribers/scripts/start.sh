#!/usr/bin/env bash
set -euo pipefail
docker compose up --build -d
printf '\nSubscribers:\n'
docker compose ps inventory-subscriber billing-subscriber notification-subscriber
printf '\nFollow all subscriber logs with:\n  docker compose logs -f inventory-subscriber billing-subscriber notification-subscriber\n'
