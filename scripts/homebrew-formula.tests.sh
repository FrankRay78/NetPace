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
# It asserts the call before answering it. That single `gh release view` is the script's whole
# contract with GitHub, so a stub answering any argv would stay green if the version stopped
# being passed — and `gh release view --json assets` with no tag reads the *latest* release,
# which is the wrong one on a retry for an older version.
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

# `curl` stub: answers each download with the URL it was asked for, as the archive's bytes. The
# real sha256sum then runs over them, so every expected checksum below is computed the same way
# from the same URL — which is what makes a transposed url/sha256 pair detectable. STUB_CURL_FAIL
# holds a substring; a URL matching it answers like a server error. As with real curl, only `-f`
# turns that into a non-zero exit — without it the error page is served as the archive's bytes —
# and only `-L` follows GitHub's redirect to the bytes at all.
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

# The checksum the script must emit for an archive, given the stub answers its download with the
# URL itself. Computed here rather than hard-coded so the cases assert the pairing of URL to
# checksum, which is the part a reader of the formula cannot verify by eye.
sha_of() { printf '%s' "$1" | sha256sum | cut -d' ' -f1; }

# A complete 0.9.0 release's asset list, trimmed to the entries the formula reads, with the
# `state` and `size` real asset objects carry. The Windows and non-AOT archives are present
# deliberately: the script must pick the four Unix AOT archives out of a full release, not assume
# a release holds only what it wants. The leading `.sha256` entry is the one name here a NetPace
# release does not carry — release-binaries.yml attaches no checksum sidecars. It is a synthetic
# near-miss, placed ahead of the archive it names so that selection has to be an exact match: a
# prefix match would take this URL, and the exact-equality assertions below would catch it.
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
# Each of these greps text only the guard itself emits. Matching the version back out of the
# message would pass with the guard deleted, because every later failure path echoes the version
# inside the archive name it could not find.
# An empty version reaches the arity check as one argument, so it is the version guards that must
# reject it. Pinned on its own message, which is the only thing distinguishing it from the
# MAJOR.MINOR.PATCH guard that would otherwise catch it.
run "" "STUB_ASSETS=$(assets_complete)"
ok "empty version → rejected as empty" '[ "$RC" != 0 ] && grep -q "must not be empty" <<<"$OUTPUT"'
run "refs/tags/0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "refs/tags/ prefix → rejected" '[ "$RC" != 0 ] && grep -q "not a ref" <<<"$OUTPUT"'
run "v0.9.0" "STUB_ASSETS=$(assets_complete)"
ok "v prefix → rejected" '[ "$RC" != 0 ] && grep -q "carry no .v. prefix" <<<"$OUTPUT"'

# release-binaries.yml also builds on release-candidate tags, so a candidate arrives here behind a
# green release run; any other non-release version can arrive only by hand. The tap follows plain
# X.Y.Z releases only: pushing a candidate to it publishes that candidate to every `brew upgrade` as
# the current NetPace.
# Each tag is given archives named after itself, as release-binaries.yml would attach, so that
# with the guard removed the script would find them — and "emits no formula" is what catches it.
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
# Asserting the url and the checksum on adjacent lines is what catches a transposition: four
# correct urls and four correct checksums, paired wrongly, would satisfy the counts above and
# leave `brew` reporting a checksum mismatch to every user on two of the four platforms.
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
# Which rid ends up under which `on_macos`/`on_intel` pair is the whole of what `brew install`
# resolves on, and none of the assertions above pin it: they check that each url appears somewhere
# with its own checksum beside it, and that the block openers appear the right number of times.
# Swapping the os blocks of all four platforms, or emitting one os block per architecture instead
# of grouping them, satisfies every one of them while sending an Intel Mac the arm64 archive. So
# the nesting is compared as a whole, in emission order.
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
# Nothing above counts `end`, so dropping the one that closes the final os block leaves a formula
# whose blocks do not balance — unparseable Ruby that every assertion above still passes, and that
# surfaces for the first time as a failed `brew install` on all three verify runners.
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
# in the formula that 404s for every user who installs it.
# Each clause of the check is exercised on its own, so deleting either one turns a case red.
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
# The checksum is taken over the bytes the archive actually serves, so a download that fails must
# stop the formula rather than leave a blank or stale sha256 — `brew` would report that to the
# user as a corrupt download, pointing at their network rather than at the release.
run "0.9.0" "STUB_ASSETS=$(assets_complete)" "STUB_CURL_FAIL=osx-arm64-aot"
ok "download fails → script fails" '[ "$RC" != 0 ]'
ok "download fails → names the archive it could not hash" 'grep -q "netpace-0.9.0-osx-arm64-aot.tar.gz" <<<"$OUTPUT"'
# The archive name above also appears in the stub's own stderr, so the generator's diagnostic is
# pinned on its own wording: without it, `set -e` and `pipefail` still fail the run, but real
# `curl -f` prints only `curl: (22) … 404`, which names no archive.
ok "download fails → says it could not download" 'grep -q "could not download netpace-0.9.0-osx-arm64-aot.tar.gz" <<<"$OUTPUT"'
ok "download fails → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'
ok "download fails → emits no empty checksum" '! grep -q "sha256 \"\"" <<<"$OUTPUT"'

echo "gh failure:"
# The message must blame reading the release, not a missing archive: with the error handling
# removed the script dies anyway, on all four architectures being absent, and sends whoever reads
# the auto-raised issue to check release assets that are in fact fine.
run "0.9.0" "STUB_FAIL=1" "STUB_ASSETS=$(assets_complete)"
ok "gh fails → script fails" '[ "$RC" != 0 ]'
ok "gh fails → blames the release read" 'grep -q "could not read release" <<<"$OUTPUT"'
ok "gh fails → does not blame a missing archive" '! grep -q "is missing" <<<"$OUTPUT"'
ok "gh fails → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "unreadable asset list:"
# gh exiting zero with something that is not JSON. jq fails on unparseable input before any filter
# runs, and without a diagnostic of its own it surfaces as a bare parse error with no mention of
# the version. Asserted on the parse message and against the missing-archive one, because both end
# in "nothing published": an object that lists no assets reads as missing archives,
# which the empty-release case above already covers.
run "0.9.0" 'STUB_ASSETS=not json'
ok "unparseable asset list → fails" '[ "$RC" != 0 ]'
ok "unparseable asset list → blames the asset list" 'grep -q "could not parse the asset list" <<<"$OUTPUT"'
ok "unparseable asset list → does not blame a missing archive" '! grep -q "is missing" <<<"$OUTPUT"'
ok "unparseable asset list → emits no formula" '! grep -q "class Netpace" <<<"$OUTPUT"'

echo "missing tool (named, not misdiagnosed as a bad release):"
# Each run sees a PATH holding the stubs and every tool the script needs except one. The stubs are
# otherwise only prepended to the real PATH, so the real tools are always found and the guard is
# never reached. Without it a missing tool is blamed on the release — a Mac with no `sha256sum`
# reports "could not download ... to checksum it" for archives that are in fact fine.
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
# A floor, as scripts/traceability-check.tests.sh carries for the same reason: twenty-four of these
# cases come from three `for … in …` loops, and emptying any list drops the count while reporting
# zero failures and exit 0 — a matrix that shrinks to nothing and still reads green.
[ "$cases" -ge 69 ] || { echo "FAIL: expected at least 69 cases, ran $cases — did a block get dropped?" >&2; exit 1; }
