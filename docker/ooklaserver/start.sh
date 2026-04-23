#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

docker compose up -d --build

echo "Waiting for OoklaServer (up to 30s)..."
for _ in $(seq 1 30); do
  if curl -fsS http://localhost:18080/speedtest/latency.txt 2>/dev/null | grep -q "^test=test"; then
    echo
    echo "OoklaServer is ready."
    echo "Point NetPace at it with:"
    echo "  NetPace --server http://localhost:18080/speedtest/upload.php"
    exit 0
  fi
  sleep 1
done

echo "ERROR: OoklaServer did not respond correctly within 30s." >&2
docker compose logs --tail=50 ooklaserver >&2
exit 1
