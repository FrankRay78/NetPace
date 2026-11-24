#!/usr/bin/env bash
#
# start.tests.sh — standalone test matrix for the slow-link lab's start script and its wiring.
#
# The lab puts one shaped link in front of OoklaServer, LibreSpeed or both. What it does for a
# maintainer can only be seen by running it: Docker, the host kernel's netem and ifb, and two real
# servers. That is a gate rather than a test (CONVENTIONS.md, Test-driven development), and the
# lab runs recorded with the change are the RED-GREEN evidence for the scenarios below.
#
# What this matrix verifies is what can be checked without Docker, and so gated on every pull
# request: the start script's argument handling, the profile table every run reads, and the one
# wiring invariant a passing lab run cannot distinguish — which container is granted the
# capability used to shape the link.
#
# The argument cases run the real start.sh with `docker` stubbed out, so nothing here
# starts a container or needs a daemon, and a parse regression that carried on past the arguments
# reaches the stub rather than a maintainer's running lab. The stub records every call and fails
# `compose up`, which is how the server-selection cases see which services each choice asks for
# and then end, on start.sh's failed-start path. The shaping cases run the real shape.sh,
# pointed at a sandbox readiness file. The last group runs no script at all: it reads the lab's
# configuration files, because what it checks is how they are wired rather than what they do.
#
# Exits non-zero on any failure.
#
#   Usage:  docker/lab/start.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
START="$HERE/start.sh"
COMPOSE="$HERE/docker-compose.yml"
PROFILES="$HERE/link-profiles.sh"

pass=0; fail=0; OUTPUT=; RC=
# A condition that asserts nothing passes `eval` — `eval ""` and `eval "# comment"` both return
# 0 — so a dropped or commented-out condition would report ok. Refuse one rather than count it.
ok() {
  case "${2//[[:space:]]/}" in
    ''|'#'*) echo "  FAIL $1 -- the condition asserts nothing"; fail=$((fail+1)); return ;;
  esac
  if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi
}

# run [args...] — run start.sh with <args> against the stub docker, setting OUTPUT, RC and
# DOCKER_CALLS. Cleared first, so a case whose `run` was lost cannot re-assert the last result.
run() {
  OUTPUT=; RC=; DOCKER_CALLS=
  : > "$DOCKER_LOG"
  OUTPUT="$(bash "$START" "$@" 2>&1)"; RC=$?
  DOCKER_CALLS="$(cat "$DOCKER_LOG")"
}

# error_line — the ERROR line alone. Every refusal prints usage, which names every profile and
# both server flags, so a check against the whole output would pass whatever the error said.
error_line() { grep -m1 "^ERROR:" <<<"$OUTPUT"; }

# up_services — the services the stubbed `docker compose up` was asked for, sorted onto one line.
up_services() {
  grep "^compose up " <<<"$DOCKER_CALLS" | head -1 | tr ' ' '\n' \
    | grep -vE "^(compose|up|-d|--build)$" | sort | tr '\n' ' ' | sed 's/ $//'
}

for f in "$START" "$COMPOSE" "$PROFILES"; do
  [ -f "$f" ] || { echo "FAIL -- $f is missing, so the lab cannot be checked." >&2; exit 1; }
done

# Read from the table itself, so the cases cannot drift from it. Sourced in a subshell, or a case
# could pass off a leftover variable as a read.
ALL_PROFILES="$(. "$PROFILES"; echo $LINK_PROFILES)"
# An empty list would make every case that iterates it pass having checked nothing: a `for` over
# no words runs its body no times and succeeds. Refuse it here rather than report that green.
[ -n "$ALL_PROFILES" ] \
  || { echo "FAIL -- $PROFILES defines no profiles, so the cases below would check nothing." >&2; exit 1; }

# A sandbox for the readiness file shape.sh writes, so the shaping cases can run off-container.
SB="$(mktemp -d)" || { echo "FAIL -- could not create a sandbox directory." >&2; exit 1; }
[ -n "$SB" ] || { echo "FAIL -- mktemp -d produced no path." >&2; exit 1; }
trap 'rm -rf "$SB"' EXIT
SHAPE_READY="$SB/shaped"

# An exported function, which bash resolves before any binary on PATH, so no case reaches the
# real docker. `compose up` fails, so a run ends on the failed-start path within a second.
DOCKER_LOG="$SB/docker-calls"
docker() {
  echo "$*" >> "$DOCKER_LOG"
  case "$1 ${2:-}" in
    "compose up") echo "stub: compose up refused" >&2; return 1 ;;
    "inspect "*)  return 1 ;;
    *)            return 0 ;;
  esac
}
export -f docker
export DOCKER_LOG

# run_shape <profile> — the real shape.sh for <profile>, with its readiness file redirected into
# the sandbox. Sets OUTPUT and RC, cleared first for the reason `run` clears them.
run_shape() {
  OUTPUT=; RC=
  OUTPUT="$(NETPACE_LINK_PROFILE="$1" NETPACE_LAB_READY="$SHAPE_READY" bash "$HERE/shape.sh" 2>&1)"
  RC=$?
}

# SCENARIO: Both providers on the same slow link
# SCENARIO: One server at full speed
# Both outcomes are gated by the recorded lab runs, per the header above; these cases verify the
# argument handling, the services each choice asks Compose for, and the configuration beneath.

echo "Help:"
run --help
ok "succeeds" '[ "$RC" = 0 ]'
ok "says a profile is optional" 'grep -qF "With no profile" <<<"$OUTPUT"'
# Subshelled: `ok` evaluates its condition in the current shell, so a bare `exit` in a loop body
# would end the matrix rather than fail the case.
ok "names every profile the table defines" \
  '(for p in $ALL_PROFILES; do grep -qF "$p" <<<"$OUTPUT" || exit 1; done)'
ok "names both servers, so a reader can start either alone" \
  'grep -qF -- "--ookla" <<<"$OUTPUT" && grep -qF -- "--librespeed" <<<"$OUTPUT"'
ok "says what running neither flag does" 'grep -qF "both servers are started" <<<"$OUTPUT"'
ok "and touches no Docker at all" '[ -z "$DOCKER_CALLS" ]'

# An argument the table does not define must be refused before anything is built: a typo that
# started an unshaped lab would be read as if it were the profile asked for.
echo "An unknown profile:"
run sattelite
ok "is refused as a usage error" '[ "$RC" = 2 ]'
ok "and its error names the profile it could not use" 'error_line | grep -qF "sattelite"'
ok "and lists the profiles that do exist" 'grep -qF "satellite" <<<"$OUTPUT" && grep -qF "radio" <<<"$OUTPUT"'
ok "before touching Docker" '[ -z "$DOCKER_CALLS" ]'

echo "Two profiles at once:"
run satellite radio
ok "is refused as a usage error rather than one of them silently winning" '[ "$RC" = 2 ]'
ok "and its error names both" 'error_line | grep -qF "satellite" && error_line | grep -qF "radio"'
ok "before touching Docker" '[ -z "$DOCKER_CALLS" ]'

echo "An unknown option:"
run --ooklaserver
ok "is refused as a usage error rather than treated as no server choice at all" '[ "$RC" = 2 ]'
ok "and its error names the option it could not use" 'error_line | grep -qF -- "--ooklaserver"'
ok "before touching Docker" '[ -z "$DOCKER_CALLS" ]'

# A server flag is not a profile. Were they parsed in one list, this would be read as a profile
# name and reported as unknown, which is a confusing way to say "you may not pick twice".
echo "A profile given twice over, once as a server flag's neighbour:"
run --librespeed satellite gprs
ok "is refused as a usage error" '[ "$RC" = 2 ]'
ok "and its error is about the profile, not the server flag" \
  'error_line | grep -qF "gprs" && ! error_line | grep -qF -- "--librespeed"'
ok "before touching Docker" '[ -z "$DOCKER_CALLS" ]'

# Which servers each choice starts. The stub's `compose up` fails, so each run below also takes
# the failed-start path: the error must name what did not start, and the lab must be removed.
echo "Naming neither server:"
run
ok "asks Compose for the link and both servers" '[ "$(up_services)" = "librespeed link ooklaserver" ]'
ok "and its error names both servers when the start fails" \
  'error_line | grep -qF "OoklaServer" && error_line | grep -qF "LibreSpeed"'
ok "and removes the lab after the failed start" \
  'sed -n "/^compose up /,\$p" <<<"$DOCKER_CALLS" | grep -q "^compose down"'

echo "--ookla alone:"
run --ookla
ok "asks Compose for the link and OoklaServer only" '[ "$(up_services)" = "link ooklaserver" ]'
ok "and its error names OoklaServer, not LibreSpeed" \
  'error_line | grep -qF "OoklaServer" && ! error_line | grep -qF "LibreSpeed"'

echo "--librespeed alone, on a profile:"
run --librespeed satellite
ok "asks Compose for the link and LibreSpeed only" '[ "$(up_services)" = "librespeed link" ]'
ok "and its error names LibreSpeed, not OoklaServer" \
  'error_line | grep -qF "LibreSpeed" && ! error_line | grep -qF "OoklaServer"'

echo "The profile table:"
ok "defines at least one profile" '[ -n "$ALL_PROFILES" ]'
# Both directions, with an impairment in each: a profile with an empty or impairment-free
# direction shapes one way only, and a measurement across it reads as if it were the whole link.
ok "gives every profile both directions, each with an impairment" \
  '(for p in $ALL_PROFILES; do
      (. "$PROFILES"; link_profile "$p" || exit 1
       [ -n "$LINK_DOWN" ] && [ -n "$LINK_UP" ] && [ -n "$LINK_MODELS" ] || exit 1
       grep -Eq "delay|rate|loss" <<<"$LINK_DOWN" || exit 1
       grep -Eq "delay|rate|loss" <<<"$LINK_UP" || exit 1) || exit 1
    done)'
# The maintainer records the figure the screen names, not the one tc applied, so a description
# that disagrees is read as fact.
#
# A copy-paste between the two direction lines is the likeliest silent edit, so both are held to
# the claim every description makes: whether the link is symmetric.
ok "shapes each profile the way its own description advertises" \
  '(for p in $ALL_PROFILES; do
      (. "$PROFILES"; link_profile "$p" || exit 1
       case "$LINK_MODELS" in
         *"each way"*) [ "${LINK_DOWN##*rate }" = "${LINK_UP##*rate }" ] || exit 1 ;;
         *)            [ "${LINK_DOWN##*rate }" != "${LINK_UP##*rate }" ] || exit 1 ;;
       esac) || exit 1
    done)'
# README.md copies these arguments verbatim for reading, and names link-profiles.sh as their one
# source. Gated, because a profile edit would otherwise leave the copy readers quote stale.
ok "documents every profile's exact arguments in README.md" \
  '(for p in $ALL_PROFILES; do
      (. "$PROFILES"; link_profile "$p" || exit 1
       grep -qF "$LINK_DOWN" "$HERE/README.md" || exit 1
       grep -qF "$LINK_UP" "$HERE/README.md" || exit 1) || exit 1
    done)'
ok "refuses a name it does not define" '! (. "$PROFILES"; link_profile no-such-profile)'

# The readiness file is what admits a server to the link, so a profile shape.sh cannot apply must
# leave it unwritten: that, not the exit code, is what keeps a server off an unshaped link.
echo "The shaping script, asked for a profile it cannot apply:"
# A readiness file left by an earlier run would admit both servers to an unshaped link, so it is
# planted first: the case below checks shape.sh removes it, not merely that it writes none.
: > "$SHAPE_READY"
run_shape no-such-profile
ok "exits non-zero" '[ "$RC" != 0 ]'
ok "and names the profile it could not use" 'grep -qF "no-such-profile" <<<"$OUTPUT"'
ok "and lists the profiles that do exist" 'grep -qF "satellite" <<<"$OUTPUT"'
ok "and leaves no readiness file, not even one from an earlier run, so no server is admitted" \
  '[ ! -f "$SHAPE_READY" ]'

# A lab run cannot tell whether a server container also holds NET_ADMIN, every figure reading the
# same either way, so it is checked here: a server that could lift its own shaping is the risk.
echo "The real lab:"
# The verdict globals are cleared: no case below runs the script, so `ok` would otherwise report
# the previous case's exit code and output against a grep that failed for its own reasons.
OUTPUT=; RC=
# The granted capability, not a mention of it: the file explains in prose why only one container
# holds NET_ADMIN, and a count of the word would be satisfied by the explanation alone.
ok "grants NET_ADMIN to exactly one container" \
  '[ "$(grep -cE "^[[:space:]]+-[[:space:]]+NET_ADMIN[[:space:]]*$" "$COMPOSE")" = 1 ]'
ok "and it is the link, not a server" \
  '[ "$(awk "/^  [a-z]/ { svc = \$1 } /^[[:space:]]+-[[:space:]]+NET_ADMIN[[:space:]]*\$/ { print svc }" "$COMPOSE")" = "link:" ]'
# Each server joins the link container's network namespace. That is the whole of "one shared
# shaped link": drop it and each server gets a network of its own, unshaped.
ok "puts both servers in the link's network namespace" \
  '[ "$(grep -c "network_mode: \"service:link\"" "$COMPOSE")" = 2 ]'
# Only the link image carries the profile table and the script that applies it, so there is one
# copy of what a profile means and no server image can disagree with it.
ok "copies the profile table and the shaping script into the link image only" \
  'grep -qE "^COPY .*link-profiles\.sh" "$HERE/Dockerfile.link" \
   && grep -qE "^COPY .*shape\.sh" "$HERE/Dockerfile.link" \
   && ! grep -qE "^COPY .*(link-profiles|shape)\.sh" "$HERE/Dockerfile.ooklaserver"'
# Without this the servers would start alongside the shaping rather than after it, and could take
# a first connection on an unshaped link, which no lab run would show.
ok "makes both servers wait for the link to be healthy" \
  '[ "$(grep -c "condition: service_healthy" "$COMPOSE")" = 2 ]'
# And that healthcheck has to test the file shape.sh actually writes. Were the two paths to drift
# the link would never report healthy, so this one fails loudly — but it fails the whole lab, and
# the reason would not be obvious from either file alone.
ok "and the healthcheck tests the readiness file shape.sh writes" \
  '(ready="$(grep -oE "/run/[^[:space:]\"]+" "$HERE/Dockerfile.link" | head -1)"
    [ -n "$ready" ] && grep -qF "$ready" "$HERE/shape.sh")'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
