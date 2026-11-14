#!/usr/bin/env bash
# Stop and remove the local LibreSpeed server.
#
#   bash stop.sh
set -euo pipefail

cd "$(dirname "$0")"

docker compose down
