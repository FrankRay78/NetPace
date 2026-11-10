#!/usr/bin/env bash
# Start the local LibreSpeed server and print the netpace command that tests against it.
#
#   ./start.sh
#
# Every start replaces whatever was running, so there is no way to end up testing against
# yesterday's container. No link profiles here: shaping lives in the OoklaServer setup
# beside this one.
set -euo pipefail

cd "$(dirname "$0")"

PORT=18081
INSTALL_URL="http://localhost:${PORT}/"
DEADLINE=60

usage() {
  echo "Usage: ./start.sh" >&2
  echo >&2
  echo "Starts the local LibreSpeed server on port ${PORT} and waits until it is ready." >&2
  echo "See README.md for what it serves and how to test against it." >&2
}

case "${1:-}" in
  -h | --help)
    usage
    exit 0
    ;;
esac

if [ "$#" -gt 0 ]; then
  echo "ERROR: start.sh takes no arguments." >&2
  echo >&2
  usage
  exit 2
fi

# Down first, not just `up -d`: tearing the container down is the one thing that cannot
# leave a server running on settings nobody asked for.
docker compose down --remove-orphans

# Ctrl-C during the pull or the wait removes the container, as a failed start does: a server
# left up would be one nobody confirmed ready.
interrupted() {
  trap - INT TERM
  echo >&2
  echo "Interrupted — removing the container." >&2
  docker compose down --remove-orphans >&2 \
    || echo "ERROR: could not remove the container — run ./stop.sh, or 'docker rm -f librespeed'." >&2
  exit 130
}
trap interrupted INT TERM

docker compose up -d

failed_start() {
  echo >&2
  echo "ERROR: $1" >&2
  echo >&2
  docker compose logs --tail=50 librespeed >&2 || echo "(could not read the container log)" >&2
  echo >&2
  echo "Removing the container." >&2
  docker compose down --remove-orphans >&2 \
    || echo "ERROR: could not remove the container — run ./stop.sh, or 'docker rm -f librespeed'." >&2
  exit 1
}

echo "Waiting for LibreSpeed (up to ${DEADLINE}s)..."

# A wall-clock budget, not a loop count: each pass costs a curl and two docker inspects,
# so counting iterations would overrun the figure printed above.
DEADLINE_AT=$(( $(date +%s) + DEADLINE ))
while [ "$(date +%s)" -lt "$DEADLINE_AT" ]; do
  status="$(docker inspect -f '{{.State.Status}}' librespeed 2>/dev/null || echo missing)"
  restarts="$(docker inspect -f '{{.RestartCount}}' librespeed 2>/dev/null || echo 0)"

  if [ "$status" = "exited" ] || [ "$status" = "dead" ] || [ "$status" = "missing" ]; then
    failed_start "the container stopped before it was ready."
  fi
  if [ "$restarts" -gt 0 ]; then
    failed_start "the container has restarted ${restarts} time(s) before becoming ready."
  fi

  # Polled through the published port, so what is confirmed is the address the maintainer is
  # about to point NetPace at, not loopback inside the container. The check is the one NetPace
  # itself makes: a success status and an empty body from the ping endpoint. A plain web server
  # answering with a page would pass a status-only check and fail NetPace's.
  if answer="$(curl -fsS --max-time 10 -o /dev/null -w '%{http_code} %{size_download}' "${INSTALL_URL}backend/empty.php" 2>/dev/null)" \
     && [ "$answer" = "200 0" ]; then
    echo
    echo "LibreSpeed is ready at ${INSTALL_URL}"
    echo "Point NetPace at it with:"
    echo "  netpace --provider librespeed --server-url ${INSTALL_URL}"
    echo
    echo "A full run on the default profile moves about 125 MiB over loopback. Use"
    echo "--profile tiny while you are only checking that it works."
    exit 0
  fi
  sleep 1
done

failed_start "LibreSpeed did not answer ${INSTALL_URL}backend/empty.php as a LibreSpeed server within ${DEADLINE}s."
