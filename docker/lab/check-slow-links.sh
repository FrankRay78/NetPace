#!/usr/bin/env bash
# Run NetPace against every slow-link profile and say which, if any, did not complete a run.
#
#   ./check-slow-links.sh
#
# For each profile in the table below it starts the lab on that profile, runs the NetPace
# built from this working tree against it with the paired NetPace profile and
# `--fail-on total`, and records the outcome. It then runs once against the full-speed
# server. A run counts as completed when `--fail-on total` exits 0: every test that ran
# had at least one request succeed.
#
# The summary shows each run's download and upload figures beside its result, because a
# run that completed can still report a figure nothing like what the link carries, and
# that is the failure this check exists to make visible.
#
# Each run's result and diagnostics are kept in a new timestamped folder under runs/,
# which is git-ignored; the folder is named in the summary.
#
# The script takes a long time. Nothing here bounds how long a run may take - a slow run
# on a slow link is the point, and a time limit here would decide what NetPace's own
# limits are for. Ctrl-C is safe: the lab is removed however the script ends.
#
# It checks OoklaServer alone: the lab's LibreSpeed server is not started.
#
# Prerequisites are the same as start.sh: see README.md.
set -euo pipefail

cd "$(dirname "$0")"

SERVER_URL="http://localhost:18080/speedtest/upload.php"

# Each line is `<lab profile>:<NetPace profile>` - the NetPace profile a user on that link
# would reasonably choose. `default` means the run is given no --profile option at all, so
# it exercises the shipped default rather than a value that happens to equal it.
#
# The pairing is what the check means. If a row stops finishing in a reasonable time, raise
# it rather than quietly moving that row to a smaller NetPace profile.
CHECKS="satellite:default
flaky:default
3g-bloated:small
gprs:tiny
radio:tiny"

RUN_DIR="runs/$(date +%Y-%m-%dT%H-%M-%S)"
BUILD_DIR=".build"

mkdir -p "$RUN_DIR"

# Removed however the script ends - a normal finish, a failure, or Ctrl-C - so no lab is
# left running behind a shaped link for the next thing on this machine.
cleanup() {
  status=$?
  trap - EXIT
  echo
  echo "Removing the lab."
  ./stop.sh >/dev/null 2>&1 \
    || echo "ERROR: could not remove the lab - run ./stop.sh." >&2
  exit "$status"
}
trap cleanup EXIT

interrupted() {
  echo >&2
  echo "Interrupted." >&2
  exit 130
}
trap interrupted INT TERM

# One build for the whole check, from the working tree, so every run measures the same
# binary and a mid-check edit cannot change what is being tested.
echo "Building NetPace from the working tree..."
dotnet build ../../src/NetPace.Console -c Release -o "$BUILD_DIR" > "$RUN_DIR/build.log" 2>&1 \
  || { echo "ERROR: the build failed - see $RUN_DIR/build.log" >&2; exit 1; }

NETPACE_DLL="$BUILD_DIR/netpace.dll"
if [ ! -f "$NETPACE_DLL" ]; then
  echo "ERROR: the build produced no $NETPACE_DLL - see $RUN_DIR/build.log" >&2
  exit 1
fi

# `<lab>|<NetPace profile>|PASS or FAIL|<exit code>|<seconds>|<download>|<upload>` per run,
# in the order run.
RESULTS=""
FAILURES=0

format_duration() {
  printf '%dm %02ds' "$(( $1 / 60 ))" "$(( $1 % 60 ))"
}

# A speed in bit/s, scaled for a person to read. `-` where the run reported none, which is
# what a run that died before the test did looks like.
format_speed() {
  if [ -z "$1" ]; then
    echo "-"
    return
  fi

  awk -v bits="$1" 'BEGIN {
    if (bits >= 1000000) { printf "%.2f Mbit/s", bits / 1000000 }
    else if (bits >= 1000) { printf "%.0f kbit/s", bits / 1000 }
    else { printf "%d bit/s", bits }
  }'
}

# The named field out of the run's one-line JSON result, or empty where it is not there.
# Showing the figures is the point of the summary: `--fail-on total` says a run completed,
# not that it measured anything like what the link carries, which is the failure this whole
# check exists to make visible.
json_number() {
  # A run that died before writing a result has no figure to read, and that is not worth a word:
  # the FAIL beside it already says what happened. The run's output
  # was redirected into the file, so the file always exists; an empty one is what no result means.
  [ -s "$1" ] || return 0

  # A result file that exists but carries no such field is a different matter, and is said out
  # loud: it means the field was renamed or the figure is not a plain integer, and a silent `-`
  # in the summary would read as "this run measured nothing" when the run measured fine.
  value=$(sed -n "s/.*\"$2\":\\([0-9][0-9]*\\).*/\\1/p" "$1" || true)

  if [ -z "$value" ]; then
    echo "WARNING: $1 carries no $2 - the figure below is unreadable, not absent." >&2
  fi

  printf '%s\n' "$value"
}

# check <lab profile, empty for the full-speed server> <NetPace profile, or `default`>
check() {
  lab_profile="$1"
  netpace_profile="$2"

  lab_label="${lab_profile:-full-speed}"
  label="$lab_label-$netpace_profile"

  echo
  echo "=============================================================="
  echo "  $lab_label link, NetPace profile $netpace_profile"
  echo "=============================================================="

  # A profile that cannot be applied in full stops the whole check rather than being
  # recorded as a NetPace failure: an unshaped or half-shaped link measures nothing.
  if [ -n "$lab_profile" ]; then
    ./start.sh "$lab_profile" --ookla
  else
    ./start.sh --ookla
  fi

  set -- --server-url "$SERVER_URL" --fail-on total --json --diagnostics
  if [ "$netpace_profile" != "default" ]; then
    set -- "$@" --profile "$netpace_profile"
  fi

  echo
  echo "Running: netpace $*"

  started_at=$(date +%s)
  if dotnet "$NETPACE_DLL" "$@" > "$RUN_DIR/$label.json" 2> "$RUN_DIR/$label.diagnostics.log"; then
    exit_code=0
  else
    exit_code=$?
  fi
  elapsed=$(( $(date +%s) - started_at ))

  if [ "$exit_code" -eq 0 ]; then
    outcome="PASS"
  else
    outcome="FAIL"
    FAILURES=$(( FAILURES + 1 ))
  fi

  download=$(format_speed "$(json_number "$RUN_DIR/$label.json" DownloadSpeedBitsPerSecond)")
  upload=$(format_speed "$(json_number "$RUN_DIR/$label.json" UploadSpeedBitsPerSecond)")

  echo "$outcome after $(format_duration "$elapsed") (exit $exit_code), down $download, up $upload"
  RESULTS="$RESULTS$lab_label|$netpace_profile|$outcome|$exit_code|$elapsed|$download|$upload
"
}

for entry in $CHECKS; do
  check "${entry%%:*}" "${entry##*:}"
done

# The full-speed server last, with the default profile: the run that has to behave as it
# always did, whatever the slow links needed.
check "" "default"

echo
echo "=============================================================="
echo "  Summary"
echo "=============================================================="
printf '%-12s  %-16s  %-6s  %-15s  %-15s  %s\n' "LINK" "NETPACE PROFILE" "RESULT" "DOWNLOAD" "UPLOAD" "RUN TIME"
echo "$RESULTS" | while IFS='|' read -r lab_label netpace_profile outcome exit_code elapsed download upload; do
  [ -n "$lab_label" ] || continue
  printf '%-12s  %-16s  %-6s  %-15s  %-15s  %s\n' \
    "$lab_label" "$netpace_profile" "$outcome" "$download" "$upload" "$(format_duration "$elapsed")"
done

echo
echo "Results and diagnostics: $RUN_DIR"

if [ "$FAILURES" -gt 0 ]; then
  echo
  echo "$FAILURES run(s) did not complete. Read the diagnostics log of each FAIL above:" >&2
  echo "each failed request carries the reason it failed." >&2
  exit 1
fi

echo
echo "Every run completed."
