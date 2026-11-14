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
# Every case runs the real scripts. The argument cases all end before the first Docker call, so
# nothing here starts a container or needs a daemon.
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

# run [args...] — run start.sh with <args>. Sets OUTPUT (stdout and stderr) and RC. Cleared
# first: they are what `ok` reads, so a case whose `run` was lost or mis-edited would otherwise
# silently re-assert the previous case's result.
run() { OUTPUT=; RC=; OUTPUT="$(bash "$START" "$@" 2>&1)"; RC=$?; }

for f in "$START" "$COMPOSE" "$PROFILES"; do
  [ -f "$f" ] || { echo "FAIL -- $f is missing, so the lab cannot be checked." >&2; exit 1; }
done

# SCENARIO: Both providers on the same slow link
# SCENARIO: One server at full speed
# The traceability markers for the labels of those names. Both outcomes are gated by the lab runs
# recorded with the change, per the header above; what the cases below verify is the argument
# handling that selects each one, and the configuration the outcomes rest on.

echo "Help:"
run --help
ok "succeeds" '[ "$RC" = 0 ]'
ok "says a profile is optional" 'grep -qF "profile" <<<"$OUTPUT"'
ok "names every profile the table defines" \
  'for p in $(. "$PROFILES"; echo $LINK_PROFILES); do grep -qF "$p" <<<"$OUTPUT" || exit 1; done'
ok "names both servers, so a reader can start either alone" \
  'grep -qF -- "--ookla" <<<"$OUTPUT" && grep -qF -- "--librespeed" <<<"$OUTPUT"'
ok "says what running neither flag does" 'grep -qiE "both" <<<"$OUTPUT"'

# An argument the table does not define must be refused before anything is built: a typo that
# started an unshaped lab would be read as if it were the profile asked for.
echo "An unknown profile:"
run sattelite
ok "is refused" '[ "$RC" != 0 ]'
ok "and names the profile it could not use" 'grep -qF "sattelite" <<<"$OUTPUT"'
ok "and lists the profiles that do exist" 'grep -qF "satellite" <<<"$OUTPUT" && grep -qF "radio" <<<"$OUTPUT"'

echo "Two profiles at once:"
run satellite radio
ok "is refused rather than one of them silently winning" '[ "$RC" != 0 ]'
ok "and names both" 'grep -qF "satellite" <<<"$OUTPUT" && grep -qF "radio" <<<"$OUTPUT"'

echo "An unknown option:"
run --ooklaserver
ok "is refused rather than treated as no server choice at all" '[ "$RC" != 0 ]'
ok "and names the option it could not use" 'grep -qF -- "--ooklaserver" <<<"$OUTPUT"'

# A server flag is not a profile. Were they parsed in one list, this would be read as a profile
# name and reported as unknown, which is a confusing way to say "you may not pick twice".
echo "A profile given twice over, once as a server flag's neighbour:"
run --librespeed satellite gprs
ok "is refused" '[ "$RC" != 0 ]'
ok "and complains about the profile, not the server flag" 'grep -qF "gprs" <<<"$OUTPUT"'

echo "The profile table:"
ok "defines at least one profile" '[ -n "$(. "$PROFILES"; echo $LINK_PROFILES)" ]'
# Both directions, with an impairment in each: a profile with an empty or impairment-free
# direction shapes one way only, and a measurement across it reads as if it were the whole link.
ok "gives every profile both directions, each with an impairment" \
  'for p in $(. "$PROFILES"; echo $LINK_PROFILES); do
     (. "$PROFILES"; link_profile "$p" || exit 1
      [ -n "$LINK_DOWN" ] && [ -n "$LINK_UP" ] && [ -n "$LINK_MODELS" ] || exit 1
      grep -Eq "delay|rate|loss" <<<"$LINK_DOWN" || exit 1
      grep -Eq "delay|rate|loss" <<<"$LINK_UP" || exit 1) || exit 1
   done'
ok "refuses a name it does not define" '! (. "$PROFILES"; link_profile no-such-profile)'

# The capability that shapes the link. A lab run cannot tell whether a server container also
# holds it: the link is shaped either way and every figure reads the same. Checked here instead,
# because a server that could lift its own shaping is the one thing the design exists to rule out.
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

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
