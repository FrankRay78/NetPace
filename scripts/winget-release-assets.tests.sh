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
#
# It asserts the call before answering it. That single `gh release view` is the script's whole
# contract with GitHub, so a stub answering any argv would stay green if the version stopped
# being passed — and `gh release view --json assets` with no tag reads the *latest* release,
# which is the wrong one on a retry for an older version.
mkdir -p "$SB/bin"
cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
if [ "$*" != "release view ${STUB_EXPECT_VERSION:-} --json assets" ]; then
  echo "gh stub: expected 'release view ${STUB_EXPECT_VERSION:-} --json assets', got 'gh $*'" >&2
  exit 64
fi
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
# `jq` stub for the Windows runner: the image's native jq.exe, run from Git Bash, writes CRLF line
# endings, and `$(...)` strips only the LF. Placed on PATH only by the case that needs it.
REAL_JQ="$(command -v jq)"
mkdir -p "$SB/crlf"
cat > "$SB/crlf/jq" <<STUB
#!/usr/bin/env bash
"$REAL_JQ" "\$@" | sed 's/\$/\r/'
STUB
chmod +x "$SB/crlf/jq"
export PATH="$SB/bin:$PATH"

DL="https://github.com/FrankRay78/NetPace/releases/download"

# The asset list a complete 0.9.0 release carries, trimmed to the entries winget reads, with the
# `state` and `size` real asset objects carry. The non-AOT and non-Windows archives are present
# deliberately: the script must pick the two AOT Windows archives out of a full release, not
# assume a release holds only what it wants. The `.sha256` entry sits ahead of the archive it
# names so that selection has to be an exact match — a prefix match would take the sidecar's URL
# and the exact-equality assertions below would catch it.
assets_complete() {
  cat <<JSON
{"assets":[
  {"name":"netpace-0.9.0-win-x64-aot.zip.sha256","state":"uploaded","size":80,"url":"$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip.sha256"},
  {"name":"netpace-0.9.0-win-x64-aot.zip","state":"uploaded","size":4017822,"url":"$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip"},
  {"name":"netpace-0.9.0-win-arm64-aot.zip","state":"uploaded","size":3760104,"url":"$DL/0.9.0/netpace-0.9.0-win-arm64-aot.zip"},
  {"name":"netpace-0.9.0-win-x64-standalone.zip","state":"uploaded","size":41228004,"url":"$DL/0.9.0/netpace-0.9.0-win-x64-standalone.zip"},
  {"name":"netpace-0.9.0-linux-x64-aot.tar.gz","state":"uploaded","size":3980112,"url":"$DL/0.9.0/netpace-0.9.0-linux-x64-aot.tar.gz"}
]}
JSON
}

pass=0; fail=0
# Every case asserts that `gh` was asked for the very version the script was handed, so the stub
# is told what to expect here rather than per case.
run() {
  local version="$1"; shift
  OUTPUT="$(env "STUB_EXPECT_VERSION=$version" "$@" bash "$SCRIPT" "$version" 2>&1)"; RC=$?
}
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC got:[$OUTPUT]"; fail=$((fail+1)); fi; }

echo "argument validation:"
OUTPUT="$(bash "$SCRIPT" 2>&1)"; RC=$?
ok "no version → usage error" '[ "$RC" != 0 ] && grep -qi usage <<<"$OUTPUT"'
# Each of these greps text only the guard itself emits. Matching the version back out of the
# message would pass with the guard deleted, because every later failure path echoes the version
# inside the archive name it could not find.
run "refs/tags/0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "refs/tags/ prefix → rejected" '[ "$RC" != 0 ] && grep -q "not a ref" <<<"$OUTPUT"'
run "v0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "v prefix → rejected" '[ "$RC" != 0 ] && grep -q "carry no .v. prefix" <<<"$OUTPUT"'

# release-binaries.yml builds on any tag push and creates the release itself, so a non-release
# tag arrives here behind a green release run. Submitting one publishes it to every user as the
# current NetPace.
# Each tag is given archives named after itself, as release-binaries.yml would attach, so that
# with the guard removed the script would find them — and "emits no URL" is what catches it.
run "test-archive-whitelist-0.0.1" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/test-archive-whitelist-0.0.1/g')"
ok "non-release tag → rejected" '[ "$RC" != 0 ] && grep -q "MAJOR.MINOR.PATCH" <<<"$OUTPUT"'
ok "non-release tag → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'
run "0.9.0-rc1" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/0.9.0-rc1/g')"
ok "release candidate → rejected" '[ "$RC" != 0 ] && grep -q "MAJOR.MINOR.PATCH" <<<"$OUTPUT"'
ok "release candidate → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo "complete release:"
run "0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "exits zero" '[ "$RC" = 0 ]'
ok "emits exactly two installers" '[ "$(grep -c . <<<"$OUTPUT")" = 2 ]'
ok "x64 line first, with architecture" '[ "$(sed -n 1p <<<"$OUTPUT")" = "$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip|x64" ]'
ok "arm64 line second, with architecture" '[ "$(sed -n 2p <<<"$OUTPUT")" = "$DL/0.9.0/netpace-0.9.0-win-arm64-aot.zip|arm64" ]'
ok "no non-AOT archive leaks in" '! grep -q standalone <<<"$OUTPUT"'
ok "no non-Windows archive leaks in" '! grep -q "tar.gz" <<<"$OUTPUT"'

echo "CRLF from a native Windows jq:"
# wingetcreate splits each argument on `|` without trimming the URL, so a trailing carriage
# return would land in the manifest's InstallerUrl.
run "0.9.0" "PATH=$SB/crlf:$PATH" "STUB_ASSETS=$(assets_complete)"
ok "exits zero" '[ "$RC" = 0 ]'
ok "x64 line carries no carriage return" '[ "$(sed -n 1p <<<"$OUTPUT")" = "$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip|x64" ]'
ok "arm64 line carries no carriage return" '[ "$(sed -n 2p <<<"$OUTPUT")" = "$DL/0.9.0/netpace-0.9.0-win-arm64-aot.zip|arm64" ]'

echo "incomplete release (must stop, not guess):"
run "0.9.0" "STUB_ASSETS=$(assets_complete | grep -v 'win-x64-aot')"
ok "x64 archive absent → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-win-x64-aot.zip" <<<"$OUTPUT"'
run "0.9.0" "STUB_ASSETS=$(assets_complete | grep -v 'win-arm64-aot')"
ok "arm64 archive absent → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-win-arm64-aot.zip" <<<"$OUTPUT"'
run "0.9.0" 'STUB_ASSETS={"assets":[]}'
ok "release with no assets → fails" '[ "$RC" != 0 ]'
ok "release with no assets → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo "stranded upload (listed, but never finished uploading):"
# A half-attached archive is still in the asset list, so matching on name alone would hand
# wingetcreate a URL that 404s for every user who installs it.
# Each clause of the check is exercised on its own, so deleting either one turns a case red.
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/"state":"uploaded","size":4017822/"state":"starter","size":4017822/')"
ok "x64 archive not in uploaded state → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-win-x64-aot.zip" <<<"$OUTPUT"'
ok "x64 archive not in uploaded state → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/"state":"uploaded","size":4017822/"state":"uploaded","size":0/')"
ok "zero-byte x64 archive → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-win-x64-aot.zip" <<<"$OUTPUT"'
ok "zero-byte x64 archive → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo "version coupling:"
# Archive names are derived from the version, so a release carrying 0.8.0's archives must read as
# missing rather than as a near-enough match. This pins that it is never published as 0.9.0.
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/0.8.0/g')"
ok "another version's archives → rejected" '[ "$RC" != 0 ]'
ok "another version's archives → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo "gh failure:"
# The message must blame reading the release, not a missing archive: with the error handling
# removed the script dies anyway, on both architectures being absent, and sends whoever reads the
# auto-raised issue to check release assets that are in fact fine.
run "0.9.0" "STUB_FAIL=1" "STUB_ASSETS=$(assets_complete)"
ok "gh fails → script fails" '[ "$RC" != 0 ]'
ok "gh fails → blames the release read" 'grep -q "could not read release" <<<"$OUTPUT"'
ok "gh fails → does not blame a missing archive" '! grep -q "is missing" <<<"$OUTPUT"'
ok "gh fails → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo "unreadable asset list:"
# gh exiting zero with the wrong shape: `.assets[]?` does not tolerate indexing an array, so this
# is a jq hard error, and without a diagnostic of its own it surfaces as a bare parse error with
# no mention of the version or of whether anything was submitted.
run "0.9.0" 'STUB_ASSETS=[]'
ok "unparseable asset list → fails" '[ "$RC" != 0 ]'
ok "unparseable asset list → says nothing was submitted" 'grep -q "nothing submitted" <<<"$OUTPUT"'
ok "unparseable asset list → emits no URL" '! grep -q "releases/download" <<<"$OUTPUT"'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
