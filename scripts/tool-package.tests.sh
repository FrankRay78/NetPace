#!/usr/bin/env bash
#
# tool-package.tests.sh — case matrix for the NetPace .NET tool package.
#
# The question this matrix answers is whether this repository's console project packs into a
# package the .NET SDK will accept as a tool: `dotnet tool install` puts a `netpace` command
# somewhere, `dnx` runs it without installing it, and the command reports the version the
# package was built with rather than the source-build placeholder. None of that is observable
# from the project file — only the real SDK commands decide it — so every case drives
# `dotnet pack`, `dotnet tool install` and `dnx` for real against a local feed.
#
# Nothing here reaches nuget.org and nothing is installed outside the sandbox: the feed is the
# only package source the install and the run are given, the install goes to a `--tool-path`
# under mktemp, and DOTNET_CLI_HOME points the global tool store at the sandbox too, so "nothing
# is left installed afterwards" is a statement about a store this matrix owns rather than about
# whatever the developer happens to have installed.
#
# The version packed is deliberately absurd (99.99.99): it matches no release, so a case that
# accidentally resolved a real NetPace package from a real feed would fail rather than pass.
#
# The released RID packages carry Native AOT binaries built on their own native runners
# (.github/workflows/release-binaries.yml, docs/RELEASING.md). AOT cannot be cross-compiled, so
# this matrix packs the framework-dependent form that one machine can build for every RID; the
# AOT pack is gated by the release workflow.
#
# Exits non-zero on any failure. Run after any edit to the tool-package properties.
#
#   Usage:  scripts/tool-package.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/.." && pwd)"
PROJECT="$REPO/src/NetPace.Console/NetPace.Console.csproj"

# The RIDs the tool promises to cover. Four carry Native AOT builds in the release workflow; the
# two macOS RIDs carry framework-dependent builds, and platforms outside this list are out of
# scope (USER_GUIDE.md).
RIDS=(win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64)

VERSION=99.99.99
PACKAGE=NetPace
COMMAND=netpace

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
FEED="$SB/feed"
TOOLS="$SB/tools"

export DOTNET_CLI_HOME="$SB/cli-home"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

pass=0; fail=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=${RC-} output:[${OUTPUT-}]"; fail=$((fail+1)); fi; }

echo "Packing $PACKAGE $VERSION from $PROJECT ..."
OUTPUT="$(dotnet pack "$PROJECT" \
  --configuration Release \
  -p:PackageVersion="$VERSION" \
  -p:Version="$VERSION" \
  -p:AssemblyVersion="$VERSION" \
  -p:FileVersion="$VERSION" \
  -p:InformationalVersion="$VERSION" \
  --output "$FEED" 2>&1)"; RC=$?
ok "the console project packs" '[ "$RC" = 0 ]'
if [ "$RC" != 0 ]; then
  echo ""
  echo "$OUTPUT" | tail -20
  echo ""
  echo "RESULT: $pass passed, $fail failed"
  exit 1
fi

echo "A package exists for every platform the tool covers:"
ok "the wrapper package carries the user-facing id" '[ -f "$FEED/$PACKAGE.$VERSION.nupkg" ]'
SETTINGS="$(python3 -I -c "
import sys, zipfile
z = zipfile.ZipFile(sys.argv[1])
print(next(z.read(n).decode('utf-8-sig') for n in z.namelist() if n.endswith('DotnetToolSettings.xml')))
" "$FEED/$PACKAGE.$VERSION.nupkg" 2>&1)"
ok "and advertises the command users type" 'grep -qF "\"$COMMAND\"" <<<"$SETTINGS"'
for rid in "${RIDS[@]}"; do
  ok "$rid has its own package" '[ -f "$FEED/$PACKAGE.$rid.$VERSION.nupkg" ]'
  ok "$rid is advertised by the wrapper" 'grep -qF "\"$rid\"" <<<"$SETTINGS"'
done

echo "Installing the packed tool:"
# SCENARIO: Tool installs and runs from its package
OUTPUT="$(cd "$SB" && dotnet tool install "$PACKAGE" --version "$VERSION" --source "$FEED" --tool-path "$TOOLS" 2>&1)"; RC=$?
ok "the install succeeds from the packed feed alone" '[ "$RC" = 0 ]'
ok "and leaves the command users type" '[ -x "$TOOLS/$COMMAND" ]'
OUTPUT="$("$TOOLS/$COMMAND" --version 2>&1)"; RC=$?
ok "the installed command reports the packed version" '[ "$RC" = 0 ] && [ "$OUTPUT" = "$VERSION" ]'
OUTPUT="$("$TOOLS/$COMMAND" --help 2>&1)"; RC=$?
ok "and serves its own help" '[ "$RC" = 0 ] && grep -qF -- "--version" <<<"$OUTPUT"'

echo "Running it once without installing:"
# SCENARIO: Tool runs once without installing
OUTPUT="$(cd "$SB" && dnx --yes --source "$FEED" "$PACKAGE@$VERSION" -- --version 2>&1)"; RC=$?
ok "dnx runs the tool and it reports the packed version" '[ "$RC" = 0 ] && grep -qxF "$VERSION" <<<"$OUTPUT"'
OUTPUT="$(cd "$SB" && dotnet tool list --global 2>&1)"; RC=$?
ok "and nothing is left installed as a global tool" '[ "$RC" = 0 ] && ! grep -qiF "$PACKAGE" <<<"$OUTPUT"'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
