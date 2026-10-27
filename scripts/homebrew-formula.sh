#!/usr/bin/env bash
#
# homebrew-formula.sh — render a published release into the `netpace` Homebrew formula.
#
# Prints the formula the tap (FrankRay78/homebrew-tap) carries as Formula/netpace.rb, covering
# macOS and Linux on x64 and ARM64 from the release's Native AOT archives. Every URL is read off
# the published release rather than built from the version, and every `sha256` is taken over the
# bytes that URL actually serves, so an archive that was never attached stops the formula here
# instead of becoming an entry that fails for everyone who installs it.
#
#   Usage:  scripts/homebrew-formula.sh <version> > netpace.rb
#
# <version> is the release tag exactly as it is published — no `refs/tags/` prefix and no `v`
# (docs/RELEASING.md, *Naming convention*). Both are rejected rather than stripped: a caller
# passing either has the wrong value in hand, and the archive names derived from it would not
# match the release.
#
# Needs `gh` authenticated against the NetPace repository (GH_TOKEN and GH_REPO, or a working
# directory inside the clone), plus `jq`, `curl` and `sha256sum`. Can be run by hand against a
# published release; every automated run comes from .github/workflows/homebrew-publish.yml.

set -euo pipefail

# Platforms the tap covers, as `<rid>|<homebrew-os-block>|<homebrew-cpu-block>`. Homebrew selects
# on operating system and then on CPU, so the order here is the order the blocks are emitted in
# and must keep each operating system's two architectures together.
#
# The AOT archive is the one offered on every platform: it carries no .NET runtime dependency and
# is roughly a seventh the size of the standalone build. macOS included — it gained Native AOT
# archives after this tap's acceptance criteria were written, and those criteria name the
# standalone build for macOS only because no AOT build existed then
# (docs/change-intent-records/2026-10-21-homebrew-tap-ships-the-aot-archives.md).
PLATFORMS=(
  "osx-x64|on_macos|on_intel"
  "osx-arm64|on_macos|on_arm"
  "linux-x64|on_linux|on_intel"
  "linux-arm64|on_linux|on_arm"
)

die() { echo "FAIL: $*" >&2; exit 1; }

if [ "$#" -ne 1 ]; then
  echo "Usage: scripts/homebrew-formula.sh <version>" >&2
  exit 2
fi

version="$1"

case "$version" in
  refs/tags/*) die "version must be the bare tag, not a ref: got '$version' (strip the refs/tags/ prefix)" ;;
  v*) die "NetPace tags carry no 'v' prefix: got '$version'" ;;
  "") die "version must not be empty" ;;
esac

# Only a MAJOR.MINOR.PATCH version is a release. release-binaries.yml also builds on release-candidate
# tags, so a candidate can arrive here behind a perfectly green release run, as can any version
# given by hand. The tap follows plain releases only: pushing a candidate to it publishes that
# candidate to every `brew upgrade` as the current NetPace.
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  die "version must be MAJOR.MINOR.PATCH: got '$version' — not a release version, nothing published"
fi

# Each tool is named before it is reached, because every call below is in a pipeline or a command
# substitution whose `|| die` blames the release: a machine with no `sha256sum` — any Mac, which
# ships `shasum -a 256` instead — otherwise reports "could not download ... to checksum it", and
# sends whoever reads that to inspect release assets which are in fact fine.
for tool in gh jq curl sha256sum; do
  command -v "$tool" >/dev/null \
    || die "$tool is required but is not on PATH — nothing published"
done

assets="$(gh release view "$version" --json assets)" \
  || die "could not read release '$version' — nothing published"

names=()
urls=()
missing=()
for platform in "${PLATFORMS[@]}"; do
  rid="${platform%%|*}"
  name="netpace-${version}-${rid}-aot.tar.gz"
  # `state` and `size` are checked alongside the name because a stranded upload still appears in
  # the asset list: an archive GitHub never finished receiving would otherwise resolve to a URL
  # that 404s for every user who installs it, which is the one outcome this script exists to stop.
  url="$(jq -r --arg n "$name" \
    'first(.assets[]? | select(.name == $n and .state == "uploaded" and .size > 0) | .url) // empty' \
    <<<"$assets")" \
    || die "could not parse the asset list for release '$version' — nothing published"
  if [ -z "$url" ]; then
    missing+=("$name")
    continue
  fi
  names+=("$name")
  urls+=("$url")
done

# Nothing is printed until every platform resolved, and nothing is downloaded either: a partial
# formula would install on the platforms it happens to name and fail on the rest, which reads to
# those users as a broken tool rather than as an incomplete release.
if [ "${#missing[@]}" -gt 0 ]; then
  die "release '$version' is missing ${missing[*]} — nothing published"
fi

# Homebrew verifies the archive it downloads against this checksum, so it is taken over the bytes
# the release actually serves: that makes the download itself part of the check, and a formula is
# never published naming a URL that could not be fetched.
shas=()
for i in "${!urls[@]}"; do
  sha="$(curl -sSLf "${urls[$i]}" | sha256sum | cut -d' ' -f1)" \
    || die "could not download ${names[$i]} to checksum it — nothing published"
  shas+=("$sha")
done

# `version` is stated rather than left to Homebrew: every url sits inside an on_os block, so there
# is no top-level url to infer it from, and the formula's own `test` asserts the installed binary
# reports it. `desc` is kept under Homebrew's 80-character audit limit.
cat <<RUBY
# Generated by scripts/homebrew-formula.sh in FrankRay78/NetPace — do not edit by hand.
# Each NetPace release overwrites this file; change the generator instead.
class Netpace < Formula
  desc "Network speed tester with server discovery, latency, download and upload tests"
  homepage "https://github.com/FrankRay78/NetPace"
  version "$version"
  license "MIT"
RUBY

previous_os=""
for i in "${!PLATFORMS[@]}"; do
  IFS='|' read -r _ os cpu <<<"${PLATFORMS[$i]}"
  if [ "$os" != "$previous_os" ]; then
    [ -n "$previous_os" ] && printf '  end\n'
    printf '\n  %s do\n' "$os"
    previous_os="$os"
  fi
  printf '    %s do\n' "$cpu"
  printf '      url "%s"\n' "${urls[$i]}"
  printf '      sha256 "%s"\n' "${shas[$i]}"
  printf '    end\n'
done
printf '  end\n'

# Every archive holds exactly one entry, the `netpace` binary (docs/RELEASING.md,
# *Archive-contents contract*), so the install is the one file and nothing needs unpacking around
# it. The `test` block is what `brew test` runs; homebrew-publish.yml asserts the version itself,
# against the binary it installed, rather than through `brew test`.
cat <<'RUBY'

  def install
    bin.install "netpace"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/netpace --version")
  end
end
RUBY
