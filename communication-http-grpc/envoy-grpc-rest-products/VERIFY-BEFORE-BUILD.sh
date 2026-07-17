#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
echo "Project directory: $PWD"
echo "gRPC package versions:"
grep -n 'Grpc\.' ProductGrpcServer/ProductGrpcServer.csproj
if grep -R '2\.82\.0' ProductGrpcServer --include='*.csproj' --include='*.props' --include='*.targets'; then
  echo 'ERROR: invalid 2.82.0 package reference found.' >&2
  exit 1
fi
echo 'OK: no 2.82.0 package reference exists.'
