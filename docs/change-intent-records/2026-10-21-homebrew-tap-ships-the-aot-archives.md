# Homebrew Tap Ships the AOT Archives, Including on macOS

**Intent:** Have the Homebrew formula offer the same build on all four platforms it covers, rather than the one the tap's acceptance criteria named for macOS.

**Behaviour:** Specified in [#392](https://github.com/FrankRay78/NetPace/issues/392) — one `brew install` per platform, `netpace --version` reporting the release. The formula is rendered by `scripts/homebrew-formula.sh`.

**Constraints:**

- #392's criteria say macOS gets the `standalone` build, written when macOS had no Native AOT archive. [#410](https://github.com/FrankRay78/NetPace/pull/410) added one before the tap existed, so the criteria's stated condition no longer holds.
- Homebrew formulae are not quarantined by macOS, so an unsigned binary still starts; this would equally be true of the standalone build.

**Decisions:**

- **Rejected: `standalone` on macOS, AOT on Linux**, as the criteria read literally. It ships a known-stale choice with nothing tracking the fix, since #410 is merged, and makes macOS users download roughly seven times the bytes for a slower start.
- **Rejected: a `cask` instead of a formula.** Casks carry the quarantine flag that formulae avoid, which is the opposite of what the no-security-warning criterion needs.

**Date:** 2026-10-21
