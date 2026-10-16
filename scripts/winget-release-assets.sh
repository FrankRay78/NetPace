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
#
# Needs `gh` authenticated against the NetPace repository (GH_TOKEN and GH_REPO, or a working
# directory inside the clone) and `jq`. Run by hand only for the first submission; every other
# run comes from .github/workflows/winget-submit.yml, which supplies both.

set -euo pipefail

# Architectures the winget package covers. The order is not significant to the manifest — each
# URL carries its own architecture, so wingetcreate never infers one — but a fixed order keeps
# the submission log readable. The .NET RID architecture and the winget architecture name
# coincide for both, so one token serves the archive name and the wingetcreate override.
ARCHITECTURES=(x64 arm64)

die() { echo "FAIL: $*" >&2; exit 1; }

if [ "$#" -ne 1 ]; then
  echo "Usage: scripts/winget-release-assets.sh <version>" >&2
  exit 2
fi

version="$1"

case "$version" in
  refs/tags/*) die "version must be the bare tag, not a ref: got '$version' (strip the refs/tags/ prefix)" ;;
  v*) die "NetPace tags carry no 'v' prefix: got '$version'" ;;
  "") die "version must not be empty" ;;
esac

# Only a MAJOR.MINOR.PATCH version is a release. release-binaries.yml builds on any tag push, and
# creates the GitHub Release itself, so a test tag or a release candidate arrives here behind a
# perfectly green release run. Submitting one publishes it to every `winget upgrade` as the
# current NetPace, and only a moderated pull request upstream can take it back.
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  die "version must be MAJOR.MINOR.PATCH: got '$version' — not a release version, nothing submitted"
fi

assets="$(gh release view "$version" --json assets)" \
  || die "could not read release '$version' — nothing submitted"

urls=()
missing=()
for arch in "${ARCHITECTURES[@]}"; do
  name="netpace-${version}-win-${arch}-aot.zip"
  # `state` and `size` are checked alongside the name because a stranded upload still appears in
  # the asset list: an archive GitHub never finished receiving would otherwise resolve to a URL
  # that 404s for every user who installs it, which is the one outcome this script exists to stop.
  url="$(jq -r --arg n "$name" \
    'first(.assets[]? | select(.name == $n and .state == "uploaded" and .size > 0) | .url) // empty' \
    <<<"$assets")" \
    || die "could not parse the asset list for release '$version' — nothing submitted"
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
  die "release '$version' is missing ${missing[*]} — nothing submitted"
fi

printf '%s\n' "${urls[@]}"
