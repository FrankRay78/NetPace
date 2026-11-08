#!/usr/bin/env bash
# Start the local OoklaServer, optionally behind one of the fixed slow-link profiles.
#
#   ./start.sh              full-speed local server, as before
#   ./start.sh satellite    the same server behind a geostationary satellite link
#
# Every start replaces whatever was running with exactly the requested state, so there
# is no way to end up on yesterday's profile. To change profile, start again on another.
set -euo pipefail

cd "$(dirname "$0")"

# shellcheck source=link-profiles.sh
. ./link-profiles.sh

PROFILE="${1:-}"

usage() {
  echo "Usage: ./start.sh [profile]" >&2
  echo >&2
  echo "With no profile, starts the full-speed local server." >&2
  echo >&2
  echo "Available profiles:" >&2
  for p in $LINK_PROFILES; do
    link_profile "$p" || LINK_MODELS="(no definition in link-profiles.sh)"
    printf '  %-12s %s\n' "$p" "$LINK_MODELS" >&2
  done
  echo >&2
  echo "See README.md for what each profile models and the per-direction values." >&2
}

if [ "$#" -gt 1 ]; then
  echo "ERROR: one profile at a time." >&2
  echo >&2
  usage
  exit 2
fi

case "$PROFILE" in
  -h | --help)
    usage
    exit 0
    ;;
esac

if [ -n "$PROFILE" ] && ! link_profile "$PROFILE"; then
  echo "ERROR: unknown link profile '$PROFILE'." >&2
  echo >&2
  usage
  exit 2
fi

# A shaped link makes every step slower, including the readiness poll itself: on the
# radio profile a bare latency.txt costs several round trips at 9.6 kbit with loss.
if [ -n "$PROFILE" ]; then
  DEADLINE=120
else
  DEADLINE=30
fi

# Down first, not just `up -d`: the profile travels as an environment variable, and
# tearing the container down is the one thing that cannot leave a running server
# shaped differently from what was asked for.
docker compose down --remove-orphans

# Ctrl-C during the build or the wait removes the container, as a failed start does: a
# server left up would be one nobody confirmed ready, and a profile that cannot be
# applied would be left restart-looping under `restart: unless-stopped`.
interrupted() {
  trap - INT TERM
  echo >&2
  echo "Interrupted — removing the container." >&2
  docker compose down --remove-orphans >&2 \
    || echo "ERROR: could not remove the container — run ./stop.sh, or 'docker rm -f ooklaserver'." >&2
  exit 130
}
trap interrupted INT TERM

export NETPACE_LINK_PROFILE="$PROFILE"
docker compose up -d --build

# A profile that cannot be applied in full exits the container non-zero from shape.sh.
# `restart: unless-stopped` then cycles it, so a failed start shows up as a container
# that is not running, or one that has already been restarted.
failed_start() {
  echo >&2
  echo "ERROR: $1" >&2
  echo >&2
  docker compose logs --tail=50 ooklaserver >&2 || echo "(could not read the container log)" >&2
  echo >&2
  echo "Removing the container." >&2
  docker compose down --remove-orphans >&2 \
    || echo "ERROR: could not remove the container — run ./stop.sh, or 'docker rm -f ooklaserver'." >&2
  exit 1
}

if [ -n "$PROFILE" ]; then
  echo "Waiting for OoklaServer on the '$PROFILE' link (up to ${DEADLINE}s)..."
else
  echo "Waiting for OoklaServer (up to ${DEADLINE}s)..."
fi

# A wall-clock budget, not a loop count: each pass costs a curl and two docker inspects,
# so counting iterations would overrun the figure printed above many times over.
DEADLINE_AT=$(( $(date +%s) + DEADLINE ))
while [ "$(date +%s)" -lt "$DEADLINE_AT" ]; do
  status="$(docker inspect -f '{{.State.Status}}' ooklaserver 2>/dev/null || echo missing)"
  restarts="$(docker inspect -f '{{.RestartCount}}' ooklaserver 2>/dev/null || echo 0)"

  if [ "$status" = "exited" ] || [ "$status" = "dead" ] || [ "$status" = "missing" ]; then
    failed_start "the container stopped before it was ready."
  fi
  if [ "$restarts" -gt 0 ]; then
    failed_start "the container has restarted ${restarts} time(s) before becoming ready."
  fi

  # Polled through the published port, so what is confirmed is the address the
  # maintainer is about to point NetPace at, not loopback inside the container.
  if curl -fsS --max-time 10 http://localhost:18080/speedtest/latency.txt 2>/dev/null | grep -q "^test=test"; then
    echo
    if [ -n "$PROFILE" ]; then
      # The values below come from the argument, so take the claim from the container
      # itself: shape.sh says this only once both directions are shaped and read back.
      # Captured, not piped into `grep -q`: grep would exit on the match while compose is
      # still writing OoklaServer's later lines, and under pipefail compose's SIGPIPE
      # would fail a server that was shaped correctly.
      shape_log="$(docker compose logs ooklaserver 2>/dev/null || true)"
      case "$shape_log" in
        *"both directions shaped"*) ;;
        *) failed_start "the container is serving, but its log does not say both directions were shaped." ;;
      esac
      echo "OoklaServer is ready on the '$PROFILE' link — $LINK_MODELS"
      echo "  download (container egress):  netem $LINK_DOWN"
      echo "  upload   (container ingress): netem $LINK_UP"
      echo
      echo "Automatic server selection is NOT slowed: the server list still comes from"
      echo "the public internet at full speed, so use --server to cross the slow link."
      echo
      echo "Point NetPace at it with:"
      echo "  netpace --server http://localhost:18080/speedtest/upload.php --profile tiny"
    else
      echo "OoklaServer is ready — no link profile in effect."
      echo "Point NetPace at it with:"
      echo "  netpace --server http://localhost:18080/speedtest/upload.php"
    fi
    exit 0
  fi
  sleep 1
done

failed_start "OoklaServer did not respond correctly within ${DEADLINE}s."
