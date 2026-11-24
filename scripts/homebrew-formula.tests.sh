#!/usr/bin/env bash
#
# homebrew-formula.tests.sh — test matrix for homebrew-formula.sh.
#
# The script renders a published release's Native AOT archives into the `netpace` Homebrew
# formula the tap carries. Everything a user ends up running comes out of it, so the matrix
# drives both of its inputs through stubs on PATH: `gh` for the release's asset list, and `curl`
# for the bytes each archive's `sha256` is taken over. The interesting cases are the ones where
# an archive is absent, carries another version, or cannot be downloaded — a formula that names a
# URL which 404s, or pairs a URL with the wrong checksum, fails for every user who installs it,
# and `brew` reports it as a corrupt download rather than as a bad release.
#
#   Usage:  scripts/homebrew-formula.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/homebrew-formula.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
# Checked before anything is written under it: an empty `SB` would turn every path below into an
# absolute one, so the stubs would be written to /bin rather than to the sandbox.
[ -n "$SB" ] && [ -d "$SB" ] || { echo "FAIL: mktemp -d gave no usable directory." >&2; exit 1; }

mkdir -p "$SB/bin"

# `gh` stub: emits the asset list named by STUB_ASSETS, or fails like a missing release when
# STUB_FAIL is set. Placed first on PATH so the script under test cannot reach the real gh.
#
# It asserts the call before answering it: a stub answering any argv would stay green if the
# version stopped being passed, and no tag reads the *latest* release.
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

# `curl` stub: answers each download with the URL it was asked for, as the archive's bytes, so a
# transposed url/sha256 pair is detectable.
#
# STUB_CURL_FAIL holds a substring, and a URL matching it answers like a server error. As with
# real curl, only `-f` makes that a non-zero exit and only `-L` follows GitHub's redirect.
cat > "$SB/bin/curl" <<'STUB'
#!/usr/bin/env bash
url="${!#}"
fail=""; follow=""
for arg in "${@:1:$#-1}"; do
  case "$arg" in
    --fail) fail=1 ;;
    --location) follow=1 ;;
    --*) ;;
    -*) [[ "$arg" == *f* ]] && fail=1; [[ "$arg" == *L* ]] && follow=1 ;;
  esac
done
[ -n "$follow" ] || exit 0
if [ -n "${STUB_CURL_FAIL:-}" ] && [[ "$url" == *"$STUB_CURL_FAIL"* ]]; then
  if [ -n "$fail" ]; then
    echo "curl stub: refusing $url" >&2
    exit 22
  fi
  printf '<html>500 Internal Server Error</html>'
  exit 0
fi
printf '%s' "$url"
STUB
chmod +x "$SB/bin/curl"

if [ ! -x "$SB/bin/gh" ] || [ ! -x "$SB/bin/curl" ]; then
  echo "FAIL: could not make the stubs executable — every case below would be meaningless." >&2
  exit 1
fi
export PATH="$SB/bin:$PATH"

DL="https://github.com/FrankRay78/NetPace/releases/download"

# Computed here rather than hard-coded, so the cases assert the pairing of URL to checksum, which
# is the part a reader of the formula cannot verify by eye.
sha_of() { printf '%s' "$1" | sha256sum | cut -d' ' -f1; }

# A complete release's asset list, with the `state` and `size` real asset objects carry. The
# Windows and non-AOT archives are present deliberately: the script must pick its four out.
#
# The leading `.sha256` is a synthetic near-miss no NetPace release carries, placed ahead of the
# archive it names so selection has to be an exact match.
assets_complete() {
  cat <<JSON
{"assets":[
  {"name":"netpace-0.9.0-linux-x64-aot.tar.gz.sha256","state":"uploaded","size":80,"url":"$DL/0.9.0/netpace-0.9.0-linux-x64-aot.tar.gz.sha256"},
  {"name":"netpace-0.9.0-linux-x64-aot.tar.gz","state":"uploaded","size":3980112,"url":"$DL/0.9.0/netpace-0.9.0-linux-x64-aot.tar.gz"},
  {"name":"netpace-0.9.0-linux-arm64-aot.tar.gz","state":"uploaded","size":3812440,"url":"$DL/0.9.0/netpace-0.9.0-linux-arm64-aot.tar.gz"},
  {"name":"netpace-0.9.0-osx-x64-aot.tar.gz","state":"uploaded","size":4104992,"url":"$DL/0.9.0/netpace-0.9.0-osx-x64-aot.tar.gz"},
  {"name":"netpace-0.9.0-osx-arm64-aot.tar.gz","state":"uploaded","size":3901760,"url":"$DL/0.9.0/netpace-0.9.0-osx-arm64-aot.tar.gz"},
  {"name":"netpace-0.9.0-linux-x64-standalone.tar.gz","state":"uploaded","size":41228004,"url":"$DL/0.9.0/netpace-0.9.0-linux-x64-standalone.tar.gz"},
  {"name":"netpace-0.9.0-osx-arm64-standalone.tar.gz","state":"uploaded","size":40881266,"url":"$DL/0.9.0/netpace-0.9.0-osx-arm64-standalone.tar.gz"},
  {"name":"netpace-0.9.0-osx-arm64-net10.tar.gz","state":"uploaded","size":1844150,"url":"$DL/0.9.0/netpace-0.9.0-osx-arm64-net10.tar.gz"},
  {"name":"netpace-0.9.0-win-x64-aot.zip","state":"uploaded","size":4017822,"url":"$DL/0.9.0/netpace-0.9.0-win-x64-aot.zip"}
]}
JSON
}

pass=0; fail=0; cases=0
# Every case asserts that `gh` was asked for the very version the script was handed, so the stub
# is told what to expect here rather than per case.
run() {
  local version="$1"; shift
  OUTPUT="$(env "STUB_EXPECT_VERSION=$version" "$@" bash "$SCRIPT" "$version" 2>&1)"; RC=$?
}
ok() { cases=$((cases+1)); if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC got:[$OUTPUT]"; fail=$((fail+1)); fi; }

echo "argument validation:"
OUTPUT="$(bash "$SCRIPT" 2>&1)"; RC=$?
ok "no version → usage error" '[ "$RC" != 0 ] && grep -qi usage <<<"$OUTPUT"'
OUTPUT="$(bash "$SCRIPT" 0.9.0 extra 2>&1)"; RC=$?
ok "surplus argument → usage error" '[ "$RC" != 0 ] && grep -qi usage <<<"$OUTPUT"'
# Each of these greps text only the guard itself emits: matching the version back out would pass
# with the guard deleted, every later failure echoing it inside the archive name.
#
# An empty version reaches the arity check as one argument, so a version guard must reject it;
# pinned on its own message, the only thing distinguishing it from the next guard.
run "" "STUB_ASSETS=$(assets_complete)"
ok "empty version → rejected as empty" '[ "$RC" != 0 ] && grep -q "must not be empty" <<<"$OUTPUT"'
run "refs/tags/0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "refs/tags/ prefix → rejected" '[ "$RC" != 0 ] && grep -q "not a ref" <<<"$OUTPUT"'
run "v0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "v prefix → rejected" '[ "$RC" != 0 ] && grep -q "carry no .v. prefix" <<<"$OUTPUT"'

# A candidate tag arrives here behind a green release run, and pushing one publishes it to every
# `brew upgrade` as the current NetPace.
#
# Each tag is given archives named after itself, so with the guard removed the script would find
# them, and "emits no formula" is what catches it.
run "test-archive-whitelist-0.0.1" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/test-archive-whitelist-0.0.1/g')"
ok "non-release tag → rejected" '[ "$RC" != 0 ] && grep -q "MAJOR.MINOR.PATCH" <<<"$OUTPUT"'
ok "non-release tag → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'
run "0.9.0-rc1" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/0.9.0-rc1/g')"
ok "release candidate → rejected" '[ "$RC" != 0 ] && grep -q "MAJOR.MINOR.PATCH" <<<"$OUTPUT"'
ok "release candidate → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "complete release:"
run "0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "exits zero" '[ "$RC" = 0 ]'
# The class name is what `brew install netpace` resolves through the filename, so it is a
# user-typed contract rather than a detail of the generator.
ok "declares the Netpace formula class" 'grep -q "^class Netpace < Formula$" <<<"$OUTPUT"'
# Stated explicitly because every url sits inside an on_os block, where Homebrew has no url to
# infer a version from — and the formula's own test asserts `netpace --version` against it.
ok "states the release version" '[ "$(grep -c "^  version \"0.9.0\"$" <<<"$OUTPUT")" = 1 ]'
ok "installs the netpace binary" 'grep -q "bin.install \"netpace\"" <<<"$OUTPUT"'
ok "emits exactly four archive urls" '[ "$(grep -c "^      url " <<<"$OUTPUT")" = 4 ]'
ok "emits exactly four checksums" '[ "$(grep -c "^      sha256 " <<<"$OUTPUT")" = 4 ]'

echo "every platform resolved, each url paired with its own checksum:"
# Adjacent lines are what catch a transposition: four correct urls and four correct checksums,
# paired wrongly, satisfy every count above.
for entry in "osx-x64" "osx-arm64" "linux-x64" "linux-arm64"; do
  url="$DL/0.9.0/netpace-0.9.0-${entry}-aot.tar.gz"
  expected="$(sha_of "$url")"
  ok "$entry url present" 'grep -qF "      url \"$url\"" <<<"$OUTPUT"'
  ok "$entry checksum follows its own url" '[ "$(grep -A1 -F "      url \"$url\"" <<<"$OUTPUT" | sed -n 2p)" = "      sha256 \"$expected\"" ]'
done

echo "only the Unix AOT archives are offered:"
# Homebrew covers macOS and Linux, and the tap ships the native build: a standalone, net10 or
# Windows archive reaching the formula would be a user downloading the wrong thing entirely.
ok "no standalone archive leaks in" '! grep -q standalone <<<"$OUTPUT"'
ok "no framework-dependent archive leaks in" '! grep -q "net10" <<<"$OUTPUT"'
ok "no Windows archive leaks in" '! grep -q "win-" <<<"$OUTPUT"'
ok "no checksum sidecar leaks in" '! grep -q "tar.gz.sha256" <<<"$OUTPUT"'

echo "both operating systems are described:"
ok "macOS block present" '[ "$(grep -c "^  on_macos do$" <<<"$OUTPUT")" = 1 ]'
ok "Linux block present" '[ "$(grep -c "^  on_linux do$" <<<"$OUTPUT")" = 1 ]'
# A formula naming one architecture per operating system installs on the other by downloading the
# wrong binary, which runs nowhere.
ok "both architectures per os" '[ "$(grep -c "on_intel do\|on_arm do" <<<"$OUTPUT")" = 4 ]'

echo "each rid sits in the os and cpu block that selects it:"
# Which rid lands under which `on_macos`/`on_intel` pair is the whole of what `brew install`
# resolves on, and none of the assertions above pin it.
#
# Swapping all four platforms' os blocks satisfies every one of them while sending an Intel Mac
# the arm64 archive, so the nesting is compared as a whole, in emission order.
structure="$(grep -E '^  on_(macos|linux) do$|^    on_(intel|arm) do$|^      url ' <<<"$OUTPUT")"
expected_structure="$(cat <<EXPECTED
  on_macos do
    on_intel do
      url "$DL/0.9.0/netpace-0.9.0-osx-x64-aot.tar.gz"
    on_arm do
      url "$DL/0.9.0/netpace-0.9.0-osx-arm64-aot.tar.gz"
  on_linux do
    on_intel do
      url "$DL/0.9.0/netpace-0.9.0-linux-x64-aot.tar.gz"
    on_arm do
      url "$DL/0.9.0/netpace-0.9.0-linux-arm64-aot.tar.gz"
EXPECTED
)"
ok "every rid nests under the os and cpu that select it" '[ "$structure" = "$expected_structure" ]'
# Nothing above counts `end`, so dropping the one closing the final os block leaves unparseable
# Ruby that every assertion above still passes.
ok "every block is closed" '[ "$(grep -c "^ *end$" <<<"$OUTPUT")" = 9 ]'

echo "incomplete release (must stop, not guess):"
for entry in "osx-x64" "osx-arm64" "linux-x64" "linux-arm64"; do
  name="netpace-0.9.0-${entry}-aot.tar.gz"
  # grep -v on the exact line, so removing linux-x64 does not also remove linux-x64-standalone.
  run "0.9.0" "STUB_ASSETS=$(assets_complete | grep -v "\"name\":\"$name\"")"
  ok "$entry archive absent → fails naming it" '[ "$RC" != 0 ] && grep -qF "$name" <<<"$OUTPUT"'
  ok "$entry archive absent → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'
done
run "0.9.0" 'STUB_ASSETS={"assets":[]}'
# Pinned on the missing-archive message, like its neighbours: a bare non-zero exit would also be
# satisfied by the script dying for an unrelated reason, such as a stub argv mismatch.
ok "release with no assets → fails naming what is missing" '[ "$RC" != 0 ] && grep -q "is missing" <<<"$OUTPUT"'
ok "release with no assets → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "stranded upload (listed, but never finished uploading):"
# A half-attached archive is still in the asset list, so matching on name alone would put a URL
# in the formula that 404s for every user. Each clause is exercised on its own.
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/"state":"uploaded","size":4104992/"state":"starter","size":4104992/')"
ok "osx-x64 archive not in uploaded state → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-osx-x64-aot.tar.gz" <<<"$OUTPUT"'
ok "osx-x64 archive not in uploaded state → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/"state":"uploaded","size":4104992/"state":"uploaded","size":0/')"
ok "zero-byte osx-x64 archive → fails naming it" '[ "$RC" != 0 ] && grep -q "netpace-0.9.0-osx-x64-aot.tar.gz" <<<"$OUTPUT"'
ok "zero-byte osx-x64 archive → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "version coupling:"
# Archive names are derived from the version, so a release carrying 0.8.0's archives must read as
# missing rather than as a near-enough match. This pins that it is never published as 0.9.0.
run "0.9.0" "STUB_ASSETS=$(assets_complete | sed 's/0\.9\.0/0.8.0/g')"
ok "another version's archives → rejected as missing" '[ "$RC" != 0 ] && grep -q "is missing" <<<"$OUTPUT"'
ok "another version's archives → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "undownloadable archive:"
# A failed download must stop the formula rather than leave a blank or stale sha256, which `brew`
# would report as a corrupt download and point at the user's network.
run "0.9.0" "STUB_ASSETS=$(assets_complete)" "STUB_CURL_FAIL=osx-arm64-aot"
ok "download fails → script fails" '[ "$RC" != 0 ]'
ok "download fails → names the archive it could not hash" 'grep -q "netpace-0.9.0-osx-arm64-aot.tar.gz" <<<"$OUTPUT"'
# Pinned on the generator's own wording, because the archive name also appears in the stub's
# stderr and real `curl -f` prints only `curl: (22) … 404`, which names no archive.
ok "download fails → says it could not download" 'grep -q "could not download netpace-0.9.0-osx-arm64-aot.tar.gz" <<<"$OUTPUT"'
ok "download fails → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'
ok "download fails → emits no empty checksum" '! grep -q "sha256 \"\"" <<<"$OUTPUT"'

echo "gh failure:"
# The message must blame reading the release, not a missing archive: without the error handling
# the script still dies, and sends whoever reads the issue to check sound release assets.
run "0.9.0" "STUB_FAIL=1" "STUB_ASSETS=$(assets_complete)"
ok "gh fails → script fails" '[ "$RC" != 0 ]'
ok "gh fails → blames the release read" 'grep -q "could not read release" <<<"$OUTPUT"'
ok "gh fails → does not blame a missing archive" '! grep -q "is missing" <<<"$OUTPUT"'
ok "gh fails → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "unreadable asset list:"
# gh exiting zero with something that is not JSON, where jq fails before any filter runs and
# surfaces as a bare parse error naming no version.
#
# Asserted against the missing-archive message too, because both end in "nothing published".
run "0.9.0" 'STUB_ASSETS=not json'
ok "unparseable asset list → fails" '[ "$RC" != 0 ]'
ok "unparseable asset list → blames the asset list" 'grep -q "could not parse the asset list" <<<"$OUTPUT"'
ok "unparseable asset list → does not blame a missing archive" '! grep -q "is missing" <<<"$OUTPUT"'
ok "unparseable asset list → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "missing tool (named, not misdiagnosed as a bad release):"
# Each run sees every tool the script needs except one; elsewhere the stubs are only prepended to
# the real PATH, so the guard is never reached. Without it a missing tool is blamed on the release.
BASH_BIN="$(command -v bash)"
for missing_tool in gh jq curl sha256sum; do
  dir="$SB/no-$missing_tool"
  mkdir -p "$dir"
  for t in bash env cut cat jq sha256sum; do
    [ "$t" = "$missing_tool" ] || ln -sf "$(command -v "$t")" "$dir/$t"
  done
  for t in gh curl; do
    [ "$t" = "$missing_tool" ] || ln -sf "$SB/bin/$t" "$dir/$t"
  done
  OUTPUT="$(env STUB_EXPECT_VERSION=0.9.0 "STUB_ASSETS=$(assets_complete)" PATH="$dir" "$BASH_BIN" "$SCRIPT" 0.9.0 2>&1)"; RC=$?
  ok "$missing_tool absent → fails naming it" '[ "$RC" != 0 ] && grep -q "$missing_tool is required" <<<"$OUTPUT"'
  ok "$missing_tool absent → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'
done

echo ""
echo "RESULT: $cases cases, $pass passed, $fail failed"
[ "$fail" = 0 ] || exit 1
# A floor, as the sibling matrices carry: most of these cases come from `for … in …` loops, and
# emptying any list drops the count while reporting zero failures and exit 0.
[ "$cases" -ge 69 ] || { echo "FAIL: expected at least 69 cases, ran $cases — did a block get dropped?" >&2; exit 1; }
