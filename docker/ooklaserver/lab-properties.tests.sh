#!/usr/bin/env bash
#
# lab-properties.tests.sh — standalone test matrix for lab-properties.sh.
#
# The script turns off the OoklaServer settings that make the server reach out to the internet
# on its own, so that the server adds no traffic to a shaped link. It is run at image build time
# over the copy of the properties file the container serves from, and the cases below run it
# against fixture properties files, so no case depends on what the upstream bundle happens to
# contain today. The last group checks the real lab wires the script in, which is what makes the
# rest of this matrix worth anything.
#
# The end-to-end outcome is that a NetPace run started the moment `start.sh` reports the lab ready
# measures the link rather than the server's start-up. That can only be seen by running the real
# lab over a shaped link, which is a gate rather than a test (CONVENTIONS.md, Test-driven
# development), and the run is the RED-GREEN evidence recorded with the change. What is verified
# here is that the lab is configured for it, and that a release dropping either setting's name
# from the properties file fails the image build rather than quietly restoring the traffic.
#
# Exits non-zero on any failure.
#
#   Usage:  docker/ooklaserver/lab-properties.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/lab-properties.sh"

# An unguarded mktemp would leave SB empty, and every fixture would then be written to the
# filesystem root rather than to a sandbox.
SB="$(mktemp -d)" || { echo "could not create a sandbox directory." >&2; exit 1; }
[ -n "$SB" ] || { echo "mktemp -d produced no path, so there is no sandbox to write fixtures to." >&2; exit 1; }
trap 'rm -rf "$SB"' EXIT

PROPS="$SB/OoklaServer.properties"

pass=0; fail=0; OUTPUT=; RC=
# A condition that asserts nothing passes `eval` — `eval ""` and `eval "# comment"` both return
# 0 — so a dropped or commented-out condition would report ok. Refuse one rather than count it.
ok() {
  case "${2//[[:space:]]/}" in
    ''|'#'*) echo "  FAIL $1 -- the condition asserts nothing"; fail=$((fail+1)); return ;;
  esac
  if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi
}

# props <contents> — a fresh fixture properties file holding <contents>. Clears the verdict
# globals too: they are what `ok` reads, so a stale value would let a case whose `run` was lost
# or mis-edited silently re-assert the previous case's result.
props() { RC=; OUTPUT=; printf '%s' "$1" > "$PROPS"; }
# run [args...] — run the script, defaulting to the fixture file. Sets OUTPUT (stdout and
# stderr) and RC.
run() { if [ "$#" -eq 0 ]; then set -- "$PROPS"; fi; OUTPUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; }
# active <key> — the uncommented assignments of <key> in the fixture file.
active() { grep -E "^[[:space:]]*${1//./\\.}[[:space:]]*=" "$PROPS"; }

# The upstream bundle ships both settings documented but commented out, so the server takes its
# default for each, which is on. This is the shape the image actually meets.
UPSTREAM=$'# Enable auto updates (default)\n#\n# OoklaServer.enableAutoUpdate = true\n\n# Enable Let\'s Encrypt certificate generation (default)\n#\n# OoklaServer.ssl.useLetsEncrypt = true\n\nopenSSL.server.minimumTLSProtocol = 1.2\n'

# SCENARIO: A run straight after start measures the link
# The traceability marker for the label of that name. The outcome itself is gated by the lab run
# recorded with the change, per the header above; what the cases below verify is the configuration
# the outcome rests on.
echo "An upstream properties file that leaves both settings at their defaults:"
props "$UPSTREAM"
run
ok "succeeds" '[ "$RC" = 0 ]'
ok "turns Let's Encrypt certificate generation off, once" \
  '[ "$(active OoklaServer.ssl.useLetsEncrypt | wc -l)" = 1 ] && active OoklaServer.ssl.useLetsEncrypt | grep -qE "=[[:space:]]*false[[:space:]]*$"'
ok "turns auto update off, once" \
  '[ "$(active OoklaServer.enableAutoUpdate | wc -l)" = 1 ] && active OoklaServer.enableAutoUpdate | grep -qE "=[[:space:]]*false[[:space:]]*$"'
ok "leaves the rest of the file alone" 'grep -qF "openSSL.server.minimumTLSProtocol = 1.2" "$PROPS"'
ok "leaves the upstream documentation of each setting in place" \
  'grep -qF "# OoklaServer.enableAutoUpdate = true" "$PROPS" && grep -qF "# OoklaServer.ssl.useLetsEncrypt = true" "$PROPS"'
ok "says what it changed" 'grep -qF "OoklaServer.ssl.useLetsEncrypt" <<<"$OUTPUT" && grep -qF "OoklaServer.enableAutoUpdate" <<<"$OUTPUT"'

echo "A properties file that already sets one of them on:"
props "$UPSTREAM"$'\nOoklaServer.enableAutoUpdate = true\n'
run
ok "succeeds" '[ "$RC" = 0 ]'
ok "leaves exactly one assignment, off, rather than two the server picks between" \
  '[ "$(active OoklaServer.enableAutoUpdate | wc -l)" = 1 ] && active OoklaServer.enableAutoUpdate | grep -qE "=[[:space:]]*false[[:space:]]*$"'

echo "Run twice over the same file:"
props "$UPSTREAM"
run
ok "the first run succeeds" '[ "$RC" = 0 ]'
run
ok "the second run succeeds" '[ "$RC" = 0 ]'
ok "still one assignment of each" \
  '[ "$(active OoklaServer.ssl.useLetsEncrypt | wc -l)" = 1 ] && [ "$(active OoklaServer.enableAutoUpdate | wc -l)" = 1 ]'

# Each key is checked in its own case: the loop dies on the first key it cannot find, so a fixture
# missing only the first one would never reach the second, and a release that renamed the second
# would go unnoticed.
echo "A properties file that does not mention the first of the settings:"
props $'# Enable auto updates (default)\n# OoklaServer.enableAutoUpdate = true\n'
run
ok "fails rather than report a setting turned off that the server never reads" '[ "$RC" != 0 ]'
ok "names the setting it could not find" 'grep -qF "OoklaServer.ssl.useLetsEncrypt" <<<"$OUTPUT"'

echo "A properties file that does not mention the second of the settings:"
props $'# Enable Let\'s Encrypt certificate generation (default)\n# OoklaServer.ssl.useLetsEncrypt = true\n'
run
ok "fails rather than report a setting turned off that the server never reads" '[ "$RC" != 0 ]'
ok "names the setting it could not find" 'grep -qF "OoklaServer.enableAutoUpdate" <<<"$OUTPUT"'

# A valid fixture throughout, so the argument is the only thing that can fail these runs. The
# exit code is asserted rather than just non-zero, because that is what separates "the arguments
# were rejected" (2) from "the edit could not be made" (1).
echo "Arguments:"
props "$UPSTREAM"
run /no/such/properties/file
ok "a file that is not there fails" '[ "$RC" = 1 ]'
ok "and says which path it could not use" 'grep -qF "/no/such/properties/file" <<<"$OUTPUT"'
run "$SB"
ok "a path that exists but is not a file fails" '[ "$RC" = 1 ]'
props "$UPSTREAM"
OUTPUT="$(bash "$SCRIPT" 2>&1)"; RC=$?
ok "no file named at all is a usage error" '[ "$RC" = 2 ]'
props "$UPSTREAM"
run "$PROPS" "$PROPS"
ok "a second argument is a usage error rather than ignored" '[ "$RC" = 2 ]'

# The whole RUN instruction, not the filename: a bare grep for the filename is satisfied by the
# COPY line below, so it stays green when the image copies the script in and never runs it. The
# argument is part of the assertion because the Dockerfile holds two properties files, and running
# over OoklaServer.properties.default would leave the served copy untouched.
echo "The real lab:"
props "$UPSTREAM"
ok "the image runs the script over the properties file it serves" \
  'grep -qE "^RUN[[:space:]]+bash[[:space:]]+lab-properties\.sh[[:space:]]+OoklaServer\.properties[[:space:]]*$" "$HERE/Dockerfile"'
ok "and copies it in" 'grep -E "^COPY" "$HERE/Dockerfile" | grep -qF "lab-properties.sh"'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
