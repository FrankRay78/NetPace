#!/usr/bin/env bash
#
# winget-release-assets.sh — resolve a release's Windows AOT archives into wingetcreate arguments.
#
# Prints one `<url>|<architecture>` line per installer the winget package offers, in a fixed
# architecture order, for `wingetcreate update --urls`. Every URL is read off the published
# release rather than built from the version, so an archive that was never attached stops the
# submission here instead of becoming a manifest entry that 404s for everyone who installs it.
#
#   Usage:  scripts/winget-release-assets.sh <version>
#
# <version> is the release tag exactly as it is published — no `refs/tags/` prefix and no `v`
# (docs/RELEASING.md, *Naming convention*). Both are rejected rather than stripped: a caller
# passing either has the wrong value in hand, and the archive names derived from it would not
# match the release.

set -euo pipefail

# Architectures the winget package covers, in the order the manifest's installer nodes carry
# them. The .NET RID architecture and the winget architecture name coincide for both, so one
# token serves the archive name and the wingetcreate override.
ARCHITECTURES=(x64 arm64)

die() { echo "FAIL: $*" >&2; exit 1; }

if [ "$#" -ne 1 ]; then
  echo "Usage: scripts/winget-release-assets.sh <version>" >&2
  exit 2
fi

version="$1"

case "$version" in
  refs/tags/*) die "version must be the bare tag, not a ref: got '$version' (strip the refs/tags/ prefix)" ;;
  v*)          die "NetPace tags carry no 'v' prefix: got '$version'" ;;
  "")          die "version must not be empty" ;;
esac

assets="$(gh release view "$version" --json assets)" \
  || die "could not read release '$version' — nothing submitted"

urls=()
missing=()
for arch in "${ARCHITECTURES[@]}"; do
  name="netpace-${version}-win-${arch}-aot.zip"
  url="$(jq -r --arg n "$name" 'first(.assets[]? | select(.name == $n) | .url) // empty' <<<"$assets")"
  if [ -z "$url" ]; then
    missing+=("$name")
    continue
  fi
  urls+=("${url}|${arch}")
done

# Nothing is printed until every architecture resolved: a caller that pipes this into
# wingetcreate must not receive a partial installer list, which would submit a manifest
# describing fewer architectures than the package promises.
if [ "${#missing[@]}" -gt 0 ]; then
  die "release '$version' is missing $(printf '%s ' "${missing[@]}")— nothing submitted"
fi

printf '%s\n' "${urls[@]}"
