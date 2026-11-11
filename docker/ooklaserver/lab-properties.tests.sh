#!/usr/bin/env bash
#
# lab-properties.tests.sh — standalone test matrix for lab-properties.sh.
#
# The script turns off the OoklaServer settings that make the server reach out to the internet
# on its own, so that nothing but NetPace's own traffic crosses a shaped link. It is run at image
# build time against the properties file the upstream bundle ships, and the cases below run it
# against fixture properties files, so no case depends on what that bundle happens to contain
# today. The last group checks the real lab wires the script in, which is what makes the rest of
# this matrix worth anything.
#
# The end-to-end outcome — a NetPace run started the moment `start.sh` reports the lab ready
# measures the link rather than the server's start-up — can only be seen by running the real lab
# over a shaped link, which is a gate rather than a test (CONVENTIONS.md, Test-driven
# development). That run is the RED-GREEN evidence on the pull request; what is verified here is
# that the lab is configured for it, and that an upstream rename of either setting fails the
# image build rather than quietly restoring the traffic.
#
# Exits non-zero on any failure.
#
#   Usage:  docker/ooklaserver/lab-properties.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/lab-properties.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT

PROPS="$SB/OoklaServer.properties"

pass=0; fail=0; OUTPUT=; RC=
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# props <contents> — a fresh fixture properties file holding <contents>.
props() { printf '%s' "$1" > "$PROPS"; }
# run [args...] — run the script, defaulting to the fixture file. Sets OUTPUT (stdout and
# stderr) and RC.
run() { if [ "$#" -eq 0 ]; then set -- "$PROPS"; fi; OUTPUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; }
# active <key> — the uncommented assignments of <key> in the fixture file.
active() { grep -E "^[[:space:]]*$(sed 's/\./\\./g' <<<"$1")[[:space:]]*=" "$PROPS"; }

# The upstream bundle ships both settings documented but commented out, so the server takes its
# default for each, which is on. This is the shape the image actually meets.
UPSTREAM=$'# Enable auto updates (default)\n#\n# OoklaServer.enableAutoUpdate = true\n\n# Enable Let\'s Encrypt certificate generation (default)\n#\n# OoklaServer.ssl.useLetsEncrypt = true\n\nopenSSL.server.minimumTLSProtocol = 1.2\n'

# SCENARIO: A run straight after start measures the link
echo "An upstream properties file that leaves both settings at their defaults:"
props "$UPSTREAM"
run
ok "succeeds" '[ "$RC" = 0 ]'
ok "turns Let's Encrypt certificate generation off, once" \
  '[ "$(active OoklaServer.ssl.useLetsEncrypt | wc -l)" = 1 ] && active OoklaServer.ssl.useLetsEncrypt | grep -qE "=[[:space:]]*false[[:space:]]*$"'
ok "turns auto update off, once" \
  '[ "$(active OoklaServer.enableAutoUpdate | wc -l)" = 1 ] && active OoklaServer.enableAutoUpdate | grep -qE "=[[:space:]]*false[[:space:]]*$"'
ok "leaves the rest of the file alone" 'grep -qF "openSSL.server.minimumTLSProtocol = 1.2" "$PROPS"'
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
run
ok "succeeds" '[ "$RC" = 0 ]'
ok "still one assignment of each" \
  '[ "$(active OoklaServer.ssl.useLetsEncrypt | wc -l)" = 1 ] && [ "$(active OoklaServer.enableAutoUpdate | wc -l)" = 1 ]'

echo "A properties file that does not mention one of the settings:"
props $'# Enable auto updates (default)\n# OoklaServer.enableAutoUpdate = true\n'
run
ok "fails rather than report a setting turned off that the server never reads" '[ "$RC" != 0 ]'
ok "names the setting it could not find" 'grep -qF "OoklaServer.ssl.useLetsEncrypt" <<<"$OUTPUT"'

echo "Arguments:"
run /no/such/properties/file
ok "a file that is not there fails" '[ "$RC" != 0 ]'
run --help-me
ok "a path that is not a file fails" '[ "$RC" != 0 ]'
OUTPUT="$(bash "$SCRIPT" 2>&1)"; RC=$?
ok "no file named at all fails" '[ "$RC" != 0 ]'
OUTPUT="$(bash "$SCRIPT" "$PROPS" "$PROPS" 2>&1)"; RC=$?
ok "a second argument fails rather than be ignored" '[ "$RC" != 0 ]'

echo "The real lab:"
ok "the image runs the script over the properties file it serves" \
  'grep -qF "lab-properties.sh" "$HERE/Dockerfile"'
ok "and copies it in" 'grep -E "^COPY" "$HERE/Dockerfile" | grep -qF "lab-properties.sh"'
OUTPUT="$(grep -nE "^[[:space:]]*(OoklaServer\.ssl\.useLetsEncrypt|OoklaServer\.enableAutoUpdate)" "$HERE/Dockerfile" || true)"
ok "the settings are named in the script, not written into the Dockerfile as well" '[ -z "$OUTPUT" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
