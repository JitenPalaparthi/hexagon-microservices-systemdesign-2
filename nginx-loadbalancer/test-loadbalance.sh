#!/usr/bin/env bash
set -euo pipefail

COUNT="${1:-10}"

for i in $(seq 1 "$COUNT"); do
  printf "Request %02d: " "$i"
  curl -s http://localhost:8080/api/shared/info | \
    sed -n 's/.*"instance":"\([^"]*\)".*/\1/p'
done
