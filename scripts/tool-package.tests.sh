#!/usr/bin/env bash
#
# tool-package.tests.sh — case matrix for the NetPace .NET tool package.
#
# The question this matrix answers is whether this repository's console project packs into a
# package the .NET SDK will accept as a tool: `dotnet tool install` puts a `netpace` command
# somewhere, `dnx` runs it without installing it, and the command reports the version the
# package was built with rather than the source-build placeholder. None of that is observable
# from the project file — only the real SDK commands decide it — so the matrix drives `dotnet pack`,
# `dotnet tool install`, `dotnet tool update` and `dnx` for real against a local feed and asserts
# against what they produce.
#
# Nothing here reaches nuget.org and nothing is installed outside the sandbox: the feed is the
# only package source the install and the run are given, the install goes to a `--tool-path`
# under mktemp, and DOTNET_CLI_HOME points the global tool store at the sandbox too, so "nothing
# is left installed afterwards" is a statement about a store this matrix owns rather than about
# whatever the developer happens to have installed. The NuGet package cache is the shared one, so a
# package restored here is cached as it would be by any build; nothing is *installed* from it.
#
# The version packed is deliberately absurd (99.99.99): it matches no release, so a case that
# accidentally resolved a real NetPace package from a real feed would fail rather than pass.
#
# All six released RID packages carry Native AOT binaries, each built on a runner matching its own
# platform (.github/workflows/publish-tool.yml, docs/RELEASING.md). AOT cannot be cross-compiled across
# operating systems, so
# this matrix packs the framework-dependent form that one machine can build for every RID; the AOT
# pack and its shape are gated by that workflow.
#
# Exits non-zero on any failure. Run after any edit to the tool-package properties.
#
#   Usage:  scripts/tool-package.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/.." && pwd)"
PROJECT="$REPO/src/NetPace.Console/NetPace.Console.csproj"

# The RIDs the tool promises to cover. Each carries a Native AOT build in the release workflow, and
# platforms outside this list are out of scope (USER_GUIDE.md).
RIDS=(win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64)

VERSION=99.99.99
# An earlier version to install first, so `dotnet tool update` has somewhere to move from.
OLD_VERSION=99.99.98
PACKAGE=NetPace
# What users type to install it. NuGet ids are case-insensitive, and the docs give the lowercase form to
# match the command, so the install, update and dnx cases use exactly what the docs tell users to type.
INSTALL_ID=netpace
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

# The same set again at the earlier version, so the update case has a version to move from. Packed into
# the same feed, which is how a user's update sees two versions of the package.
echo "Packing $PACKAGE $OLD_VERSION as well ..."
OUTPUT="$(dotnet pack "$PROJECT" \
  --configuration Release \
  -p:PackageVersion="$OLD_VERSION" \
  -p:Version="$OLD_VERSION" \
  -p:AssemblyVersion="$OLD_VERSION" \
  -p:FileVersion="$OLD_VERSION" \
  -p:InformationalVersion="$OLD_VERSION" \
  --output "$FEED" 2>&1)"; RC=$?
ok "the console project packs at an earlier version too" '[ "$RC" = 0 ]'

echo "A package exists for every platform the tool covers:"
# The cases in this block read the feed and the wrapper's own settings file, not the pack output, so the
# pack's log is cleared first rather than being reported against a failure it had nothing to do with.
# The install, update and dnx cases further down each set their own RC and OUTPUT.
OUTPUT=""; RC=0
ok "the wrapper package carries the user-facing id" '[ -f "$FEED/$PACKAGE.$VERSION.nupkg" ]'
SETTINGS="$(python3 -I -c "
import sys, zipfile
z = zipfile.ZipFile(sys.argv[1])
print(next(z.read(n).decode('utf-8-sig') for n in z.namelist() if n.endswith('DotnetToolSettings.xml')))
" "$FEED/$PACKAGE.$VERSION.nupkg" 2>&1)"; RC=$?; OUTPUT="$SETTINGS"
# Asserted on its own, so a wrapper that stops carrying a settings file is reported as that rather
# than as seven unexplained failures below with nothing in the diagnostic.
ok "and carries a tool settings file" '[ "$RC" = 0 ]'
ok "and advertises the command users type" 'grep -qF "\"$COMMAND\"" <<<"$SETTINGS"'
for rid in "${RIDS[@]}"; do
  ok "$rid has its own package" '[ -f "$FEED/$PACKAGE.$rid.$VERSION.nupkg" ]'
  ok "$rid is advertised by the wrapper" 'grep -qF "\"$rid\"" <<<"$SETTINGS"'
  # Only the host RID gets installed below, so without this the other five are checked by filename
  # alone and a package whose binary landed under the wrong path would pass.
  ENTRY="$(python3 -I -c "
import sys, zipfile
rid = sys.argv[2]
want = 'netpace.exe' if rid.startswith('win-') else 'netpace'
print(any(n.endswith('/' + rid + '/' + want) for n in zipfile.ZipFile(sys.argv[1]).namelist()))
" "$FEED/$PACKAGE.$rid.$VERSION.nupkg" "$rid" 2>&1)"
  ok "and carries the command for $rid" '[ "$ENTRY" = True ]'
done
ok "the wrapper advertises no platform the release does not publish" \
  '[ "$(grep -oF RuntimeIdentifier=\" <<<"$SETTINGS" | grep -c .)" = "${#RIDS[@]}" ]'

echo "Installing the packed tool:"
# SCENARIO: Tool installs and runs from its package
OUTPUT="$(cd "$SB" && dotnet tool install "$INSTALL_ID" --version "$VERSION" --source "$FEED" --tool-path "$TOOLS" 2>&1)"; RC=$?
ok "the install succeeds from the packed feed alone" '[ "$RC" = 0 ]'
ok "and leaves the command users type" '[ -x "$TOOLS/$COMMAND" ]'
OUTPUT="$("$TOOLS/$COMMAND" --version 2>&1)"; RC=$?
ok "the installed command reports the packed version" '[ "$RC" = 0 ] && [ "$OUTPUT" = "$VERSION" ]'
OUTPUT="$("$TOOLS/$COMMAND" --help 2>&1)"; RC=$?
ok "and serves its own help" '[ "$RC" = 0 ] && grep -qF -- "--version" <<<"$OUTPUT"'

echo "Updating an installed tool to a newer version:"
UPDATED="$SB/updated"
OUTPUT="$(cd "$SB" && dotnet tool install "$INSTALL_ID" --version "$OLD_VERSION" --source "$FEED" --tool-path "$UPDATED" 2>&1)"; RC=$?
ok "an older version installs first" '[ "$RC" = 0 ] && [ "$("$UPDATED/$COMMAND" --version)" = "$OLD_VERSION" ]'
OUTPUT="$(cd "$SB" && dotnet tool update "$INSTALL_ID" --source "$FEED" --tool-path "$UPDATED" 2>&1)"; RC=$?
ok "the update succeeds from the packed feed alone" '[ "$RC" = 0 ]'
OUTPUT="$("$UPDATED/$COMMAND" --version 2>&1)"; RC=$?
ok "and the command now reports the newer version" '[ "$RC" = 0 ] && [ "$OUTPUT" = "$VERSION" ]'

echo "Installing a prerelease:"
# SCENARIO: An installed prerelease tool reports its prerelease version
# Packed as the release workflows pack a prerelease tag: the assembly and file versions accept only
# numbers, so they take the tag without its suffix, and everything else takes the whole tag.
PRERELEASE="$VERSION-rc.1"
OUTPUT="$(dotnet pack "$PROJECT" \
  --configuration Release \
  -p:PackageVersion="$PRERELEASE" \
  -p:Version="$PRERELEASE" \
  -p:AssemblyVersion="$VERSION" \
  -p:FileVersion="$VERSION" \
  -p:InformationalVersion="$PRERELEASE" \
  --output "$FEED" 2>&1)"; RC=$?
ok "the console project packs at a prerelease version" '[ "$RC" = 0 ]'
PRE_TOOLS="$SB/prerelease"
OUTPUT="$(cd "$SB" && dotnet tool install "$INSTALL_ID" --version "$PRERELEASE" --source "$FEED" --tool-path "$PRE_TOOLS" 2>&1)"; RC=$?
ok "the prerelease installs by its version" '[ "$RC" = 0 ]'
OUTPUT="$("$PRE_TOOLS/$COMMAND" --version 2>&1)"; RC=$?
ok "and reports the prerelease version, suffix included" '[ "$RC" = 0 ] && [ "$OUTPUT" = "$PRERELEASE" ]'

echo "Running it once without installing:"
# SCENARIO: Tool runs once without installing
OUTPUT="$(cd "$SB" && dnx --yes --source "$FEED" "$INSTALL_ID@$VERSION" -- --version 2>&1)"; RC=$?
ok "dnx runs the tool and it reports the packed version" '[ "$RC" = 0 ] && grep -qxF "$VERSION" <<<"$OUTPUT"'
# Asserted against the tool-path stores this matrix owns as well as the global one: `dnx` leaving a
# command behind anywhere is the thing the scenario is about, and the global store alone cannot show it.
OUTPUT="$(cd "$SB" && dotnet tool list --global 2>&1)"; RC=$?
ok "and nothing is left installed as a global tool" '[ "$RC" = 0 ] && ! grep -qiF "$PACKAGE" <<<"$OUTPUT"'
OUTPUT="$(ls "$DOTNET_CLI_HOME/.dotnet/tools" 2>&1)"; RC=$?
ok "and the global tool store holds no command at all" '[ ! -e "$DOTNET_CLI_HOME/.dotnet/tools/$COMMAND" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
