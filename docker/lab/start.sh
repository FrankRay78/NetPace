#!/usr/bin/env bash
# Start the slow-link lab: one shaped link, with OoklaServer, LibreSpeed or both behind it.
#
#   ./start.sh                        both servers, full local speed
#   ./start.sh satellite              both servers behind a geostationary satellite link
#   ./start.sh satellite --librespeed LibreSpeed alone, behind that link
#   ./start.sh --ookla                OoklaServer alone, full local speed
#
# Every start replaces whatever was running with exactly the requested state, so there is no way
# to end up on yesterday's profile or with a server nobody asked for. To change either, start
# again. Either server failing to start removes both: a lab that is half up is one whose figures
# cannot be compared with anything.
set -euo pipefail

cd "$(dirname "$0")"

# shellcheck source=link-profiles.sh
. ./link-profiles.sh

OOKLA_PORT=18080
LIBRESPEED_PORT=18081
OOKLA_URL="http://localhost:${OOKLA_PORT}/speedtest/"
LIBRESPEED_URL="http://localhost:${LIBRESPEED_PORT}/"

# The container names docker-compose.yml fixes, so a status check here and the name a reader
# sees in `docker ps` cannot drift apart.
LINK_CONTAINER=netpace-lab-link
OOKLA_CONTAINER=netpace-lab-ooklaserver
LIBRESPEED_CONTAINER=netpace-lab-librespeed

PROFILE=""
WANT_OOKLA=""
WANT_LIBRESPEED=""

usage() {
  echo "Usage: ./start.sh [profile] [--ookla] [--librespeed]" >&2
  echo >&2
  echo "With no profile, the link runs at full local speed." >&2
  echo "With neither server named, both servers are started behind the same link." >&2
  echo >&2
  echo "Servers:" >&2
  printf '  %-14s %s\n' "--ookla" "OoklaServer, on port ${OOKLA_PORT}" >&2
  printf '  %-14s %s\n' "--librespeed" "LibreSpeed, on port ${LIBRESPEED_PORT}" >&2
  echo >&2
  echo "Available profiles:" >&2
  for p in $LINK_PROFILES; do
    link_profile "$p" || LINK_MODELS="(no definition in link-profiles.sh)"
    printf '  %-14s %s\n' "$p" "$LINK_MODELS" >&2
  done
  echo >&2
  echo "See README.md for what each profile models and the per-direction values." >&2
}

# Server flags and the profile are parsed from one list but kept apart, so a second profile is
# reported as "one profile at a time" rather than as an unknown option.
while [ "$#" -gt 0 ]; do
  case "$1" in
    -h | --help)
      usage
      exit 0
      ;;
    --ookla)
      WANT_OOKLA=1
      ;;
    --librespeed)
      WANT_LIBRESPEED=1
      ;;
    -*)
      echo "ERROR: unknown option '$1'." >&2
      echo >&2
      usage
      exit 2
      ;;
    *)
      if [ -n "$PROFILE" ]; then
        echo "ERROR: one profile at a time — '$PROFILE' and '$1' were both given." >&2
        echo >&2
        usage
        exit 2
      fi
      PROFILE="$1"
      ;;
  esac
  shift
done

# Neither server named: start both, which is the provider comparison the lab exists for.
if [ -z "$WANT_OOKLA" ] && [ -z "$WANT_LIBRESPEED" ]; then
  WANT_OOKLA=1
  WANT_LIBRESPEED=1
fi

if [ -n "$PROFILE" ] && ! link_profile "$PROFILE"; then
  echo "ERROR: unknown link profile '$PROFILE'." >&2
  echo >&2
  usage
  exit 2
fi

# `if` rather than `[ … ] && …`: under `set -e` a test that comes out false is a failed command
# on its own, and the script would exit before it ever reached Docker.
SERVICES="link"
SERVERS=""
if [ -n "$WANT_OOKLA" ]; then
  SERVICES="$SERVICES ooklaserver"
  SERVERS="OoklaServer"
fi
if [ -n "$WANT_LIBRESPEED" ]; then
  SERVICES="$SERVICES librespeed"
  SERVERS="${SERVERS:+$SERVERS and }LibreSpeed"
fi

# A shaped link makes every step slower, including the readiness poll itself: on the radio
# profile a bare latency.txt costs several round trips at 9.6 kbit with loss.
if [ -n "$PROFILE" ]; then
  DEADLINE=180
else
  DEADLINE=60
fi

# This lab replaces two earlier ones, in docker/ooklaserver/ and docker/librespeed/. A container
# either of them left running holds host port 18080 or 18081, so the lab would fail to publish
# it. Matched on the Compose project and service labels, both of which must match, so this lab's
# own ooklaserver service (project netpace-lab) is never caught by it.
#
# The old librespeed lab named its project netpace-librespeed, so that call can match nothing
# else. The old ookla lab named no project, so Compose derived one from its directory and the
# label is the generic `ooklaserver`. That call also passes the container name the old lab fixed,
# matched exactly as the optional third argument, so a contributor's own project and service of
# that name is caught only if its container carries that name too.
remove_old_lab() {
  # No `|| true`: docker ps exits 0 with no output when nothing matches, so a non-zero exit means
  # the daemon could not be reached. Letting set -e stop here says that, where carrying on would
  # report the old labs cleared and then fail on a port one of them still holds.
  old_ids="$(docker ps -aq \
    --filter "label=com.docker.compose.project=$1" \
    --filter "label=com.docker.compose.service=$2" \
    ${3:+--filter "name=^$3\$"})"
  if [ -n "$old_ids" ]; then
    echo "Removing a container left by the old '$1' lab."
    # shellcheck disable=SC2086
    docker rm -f $old_ids >/dev/null \
      || echo "ERROR: could not remove it — run 'docker rm -f $old_ids'." >&2
  fi
}
remove_old_lab ooklaserver ooklaserver ooklaserver
remove_old_lab netpace-librespeed librespeed

# Down first, not just `up -d`: the profile travels as an environment variable, and tearing the
# containers down is the one thing that cannot leave a running lab shaped differently from what
# was asked for, or carrying a server that was not.
docker compose down --remove-orphans

# Every path out of a started lab removes it, so the removal is written once. Reported on stderr
# and never fatal: the caller is already on its way out with a status of its own to return, and a
# removal that failed is something the maintainer has to be told, not something to abort on.
remove_lab() {
  # The remedy names the containers rather than ./stop.sh, which is this same command: advising it
  # here would send the maintainer back to the thing that has just failed, with the host ports
  # still held and the next start failing on a conflict that looks nothing like the real cause.
  docker compose down --remove-orphans >&2 \
    || echo "ERROR: could not remove the lab — run 'docker rm -f $LINK_CONTAINER $OOKLA_CONTAINER $LIBRESPEED_CONTAINER'." >&2
}

# Ctrl-C during the build or the wait removes what was started, as a failed start does: a lab
# left up would be one nobody confirmed ready.
interrupted() {
  trap - INT TERM
  echo >&2
  echo "Interrupted — removing the lab." >&2
  remove_lab
  exit 130
}
trap interrupted INT TERM

# Either server failing takes the whole lab down with it. Comparing two providers across one
# link is the point of the lab, and a figure from a lab that only half started cannot be
# compared with anything — so the failure is reported, named, and nothing is left running.
failed_start() {
  echo >&2
  echo "ERROR: $1" >&2
  echo >&2
  # shellcheck disable=SC2086
  docker compose logs --tail=50 $SERVICES >&2 || echo "(could not read the container logs)" >&2
  echo >&2
  echo "Removing the lab — a start either brings up everything asked for, or nothing." >&2
  remove_lab
  exit 1
}

export NETPACE_LINK_PROFILE="$PROFILE"

# Each server waits for the link container's healthcheck, which passes only once both directions
# are shaped, so a profile that cannot be applied fails here rather than starting a server. So does
# everything else that stops Compose — a held port, a failed build or pull — so the message names
# what did not start and leaves the cause to Compose's own output, which is printed above it.
# shellcheck disable=SC2086
docker compose up -d --build $SERVICES \
  || failed_start "the lab did not start, so $SERVERS is not running. Docker's output above names the cause.${PROFILE:+ If the '$PROFILE' profile could not be applied, the link log below says so.}"

if [ -n "$PROFILE" ]; then
  # The values printed at the end come from the argument, so take the claim from the container
  # itself: shape.sh says this only once both directions are shaped and read back. Captured, not
  # piped into `grep -q`: grep would exit on the match while compose is still writing later
  # lines, and under pipefail compose's SIGPIPE would fail a link that was shaped correctly.
  shape_log="$(docker compose logs link 2>/dev/null || true)"
  case "$shape_log" in
    *"both directions shaped"*) ;;
    *) failed_start "the link container is up, but its log does not say both directions were shaped." ;;
  esac
fi

# Neither check suppresses curl's own error text. The polling loop below silences it per attempt,
# and shows it once on the timeout path: a proxy inherited from the environment, a refused
# loopback connection or an exceeded --max-time are all host-side, so curl is the only thing that
# names them and none of them appear in the container logs failed_start prints.
ready_ookla() {
  curl -fsS --max-time 10 "${OOKLA_URL}latency.txt" | grep -q "^test=test"
}

# The check NetPace itself makes: a success status and an empty body from the ping endpoint. A
# plain web server answering with a page would pass a status-only check and fail every run.
ready_librespeed() {
  answer="$(curl -fsS --max-time 10 -o /dev/null -w '%{http_code} %{size_download}' \
    "${LIBRESPEED_URL}backend/empty.php")" || return 1
  [ "$answer" = "200 0" ]
}

# await <container> <ready-function> <name> <port>
#
# Polled through the published port, so what is confirmed is the address the maintainer is about
# to point NetPace at, not loopback inside the container. A wall-clock budget, not a loop count:
# each pass costs a curl and a docker inspect, so counting iterations would overrun the figure
# printed above many times over.
await() {
  container="$1"
  check="$2"
  name="$3"
  port="$4"

  echo "Waiting for $name (up to ${DEADLINE}s)..."
  deadline_at=$(( $(date +%s) + DEADLINE ))
  while [ "$(date +%s)" -lt "$deadline_at" ]; do
    for c in "$LINK_CONTAINER" "$container"; do
      status="$(docker inspect -f '{{.State.Status}}' "$c" 2>/dev/null || echo missing)"
      case "$status" in
        exited | dead | missing)
          failed_start "$name is not ready and the '$c' container is $status."
          ;;
      esac
    done

    if "$check" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done

  # One unsuppressed attempt, so the timeout carries curl's own reason for it.
  echo "The last readiness attempt reported:" >&2
  "$check" >/dev/null || true
  failed_start "$name did not answer on port ${port} within ${DEADLINE}s."
}

if [ -n "$WANT_OOKLA" ]; then
  await "$OOKLA_CONTAINER" ready_ookla OoklaServer "$OOKLA_PORT"
fi
if [ -n "$WANT_LIBRESPEED" ]; then
  await "$LIBRESPEED_CONTAINER" ready_librespeed LibreSpeed "$LIBRESPEED_PORT"
fi

# `await` watches only the server it is waiting for, so a server that answered and then exited is
# not looked at again — and the second wait can run for the whole deadline. Without this sweep the
# lab would print the ready message, and the command to use, for a container that is gone.
# OoklaServer is the one that dies late in practice: it aborts on an out-of-date warning or
# missing AES-NI, both of which happen after it has served latency.txt.
# shellcheck disable=SC2086
for c in "$LINK_CONTAINER" \
         ${WANT_OOKLA:+"$OOKLA_CONTAINER"} ${WANT_LIBRESPEED:+"$LIBRESPEED_CONTAINER"}; do
  status="$(docker inspect -f '{{.State.Status}}' "$c" 2>/dev/null || echo missing)"
  [ "$status" = "running" ] \
    || failed_start "the '$c' container is $status, so the lab did not come up in full."
done

echo
if [ -n "$PROFILE" ]; then
  echo "The lab is ready on the '$PROFILE' link — $LINK_MODELS"
  echo "  download (link egress):  netem $LINK_DOWN"
  echo "  upload   (link ingress): netem $LINK_UP"
else
  echo "The lab is ready — no link profile in effect."
fi
echo

echo "Point NetPace at it with:"
if [ -n "$WANT_OOKLA" ]; then
  echo "  netpace --server-url ${OOKLA_URL}upload.php${PROFILE:+ --profile tiny}"
fi
if [ -n "$WANT_LIBRESPEED" ]; then
  echo "  netpace --provider librespeed --server-url ${LIBRESPEED_URL}${PROFILE:+ --profile tiny}"
fi

if [ -n "$WANT_OOKLA" ] && [ -n "$WANT_LIBRESPEED" ]; then
  echo
  echo "Both servers share one link, so run them one at a time: a run against one while the"
  echo "other is being measured crosses a link the other run is already filling."
fi

if [ -n "$PROFILE" ]; then
  echo
  echo "Automatic server selection is NOT slowed: the server list still comes from the public"
  echo "internet at full speed, so use --server-url to cross the slow link."
  echo
  echo "--profile tiny is worth the habit. On the narrower profiles a default-sized transfer is"
  echo "a wait measured in hours."
else
  echo
  echo "A full run on the default profile moves about 120 MiB over loopback. Use"
  echo "--profile tiny while you are only checking that the lab works."
fi
exit 0
