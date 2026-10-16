#!/usr/bin/env bash
#
# winget-release-assets.tests.sh — test matrix for winget-release-assets.sh.
#
# The script resolves the Windows AOT archives of a published release into the
# `<url>|<architecture>` arguments wingetcreate expects. Its whole job is to refuse to hand
# wingetcreate anything it has not first seen on the release, so the matrix drives `gh` through a
# stub on PATH: the interesting cases are the ones where an asset is absent, carries another
# version, or `gh` itself fails, because each of those is a release that must stop rather than
# produce a manifest pointing at a URL that 404s for every user who installs it.
#
#   Usage:  scripts/winget-release-assets.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/winget-release-assets.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT

# `gh` stub: emits the asset list named by STUB_ASSETS, or fails like a missing release when
# STUB_FAIL is set. Placed first on PATH so the script under test cannot reach the real gh.
mkdir -p "$SB/bin"
cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
if [ -n "${STUB_FAIL:-}" ]; then
  echo "release not found" >&2
  exit 1
fi
printf '%s' "${STUB_ASSETS:-[]}"
STUB
chmod +x "$SB/bin/gh"
if [ ! -x "$SB/bin/gh" ]; then
  echo "FAIL: could not make the gh stub executable — every case below would be meaningless." >&2
  exit 1
fi
export PATH="$SB/bin:$PATH"

DL="https://github.com/FrankRay78/NetPace/releases/download"

# The asset list a complete 0.9.0 release carries, trimmed to the entries winget reads. The
# non-AOT and non-Windows archives are present deliberately: the script must pick the two AOT
# Windows archives out of a full release, not assume a release holds only what it wants.
assets_complete() {
  cat <<JSON
{"assets":[
  {"name":"netpace-0.9.0-win-x64-aot.zip","url":"$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip"},
  {"name":"netpace-0.9.0-win-arm64-aot.zip","url":"$DL/0.9.0/netpace-0.9.0-win-arm64-aot.zip"},
  {"name":"netpace-0.9.0-win-x64-standalone.zip","url":"$DL/0.9.0/netpace-0.9.0-win-x64-standalone.zip"},
  {"name":"netpace-0.9.0-linux-x64-aot.tar.gz","url":"$DL/0.9.0/netpace-0.9.0-linux-x64-aot.tar.gz"}
]}
JSON
}

pass=0; fail=0
run() {
  local version="$1"; shift
  OUTPUT="$(env "$@" bash "$SCRIPT" "$version" 2>&1)"; RC=$?
}
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC got:[$OUTPUT]"; fail=$((fail+1)); fi; }

echo "argument validation:"
OUTPUT="$(bash "$SCRIPT" 2>&1)"; RC=$?
ok "no version → usage error" '[ "$RC" != 0 ] && grep -qi usage <<<"$OUTPUT"'
run "refs/tags/0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "refs/tags/ prefix → rejected" '[ "$RC" != 0 ] && grep -q "refs/tags/" <<<"$OUTPUT"'
run "v0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "v prefix → rejected" '[ "$RC" != 0 ] && grep -qi "v0.9.0" <<<"$OUTPUT"'

echo "complete release:"
run "0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "exits zero" '[ "$RC" = 0 ]'
ok "emits exactly two installers" '[ "$(grep -c . <<<"$OUTPUT")" = 2 ]'
ok "x64 line first, with architecture" '[ "$(sed -n 1p <<<"$OUTPUT")" = "$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip|x64" ]'
ok "arm64 line second, with architecture" '[ "$(sed -n 2p <<<"$OUTPUT")" = "$DL/0.9.0/netpace-0.9.0-win-arm64-aot.zip|arm64" ]'
ok "no non-AOT archive leaks in" '! grep -q standalone <<<"$OUTPUT"'
ok "no non-Windows archive leaks in" '! grep -q "tar.gz" <<<"$OUTPUT"'

echo "incomplete release (must stop, not guess):"
run "0.9.0" "STUB_ASSETS=$(assets_complete | grep -v 'win-x64-aot')"
ok "x64 archive absent → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-win-x64-aot.zip" <<<"$OUTPUT"'
run "0.9.0" "STUB_ASSETS=$(assets_complete | grep -v 'win-arm64-aot')"
ok "arm64 archive absent → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-win-arm64-aot.zip" <<<"$OUTPUT"'
run "0.9.0" 'STUB_ASSETS={"assets":[]}'
ok "release with no assets → fails" '[ "$RC" != 0 ]'
ok "release with no assets → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo "version coupling:"
# A release carrying the previous version's archives must not be published as this version: the
# manifest would claim 0.9.0 and install 0.8.0, which no user could tell apart until it ran.
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/0.8.0/g')"
ok "another version's archives → rejected" '[ "$RC" != 0 ]'

echo "gh failure:"
run "0.9.0" "STUB_FAIL=1" "STUB_ASSETS=$(assets_complete)"
ok "gh fails → script fails" '[ "$RC" != 0 ]'
ok "gh fails → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
