# Hardening the Dependency Pipeline Against Supply-Chain Risk

**Intent:** Keep the automated dependency-update flow (monthly Dependabot PRs, human-gated, no auto-merge) but make ingesting a new package version *trustworthy* — so that a same-day compromised publish is never taken, and a known-vulnerable dependency is caught before release. Chosen over the alternative of stopping automation and bumping by hand.

**Behaviour:**
- Given: Dependabot detects a newer package version, When: that version is younger than 14 days, Then: no PR is opened until the window elapses — the project never ingests a release on its publish day.
- Given: the build pipeline runs, When: any direct or transitive dependency has a known advisory, Then: CI fails the build (`dotnet list package --vulnerable --include-transitive`) and GitHub Dependabot security alerts flag it independently of the version-update PRs.

**Constraints:**
- The existing human gate stays: Dependabot opens PRs assigned to the maintainer; nothing auto-merges. Hardening adds assurance *around* that gate, it does not replace it.
- No new package feed is introduced, so package-source-mapping is out of scope (single implicit nuget.org feed = no dependency-confusion vector to close yet).
- CodeQL is SAST over our own code and does **not** cover dependency advisories; the SCA gate is additive, not a duplicate of it.

**Decisions:**
1. **Harden the automated flow, do not go manual.** Dependabot is not the attack surface — a malicious version reaches nuget.org regardless. Going manual trades away timely known-CVE patching (the common harm) to avoid a slice of the rare injection case that the human merge gate already covers. Rejected: disabling the chore workflow.
2. **Cooldown via Dependabot config over a bespoke delay mechanism.** Native `cooldown:` keeps the policy declarative in one file the maintainer already reviews. Rejected: custom scripting / Renovate migration (Renovate's `minimumReleaseAge` is equivalent but a larger tooling change than warranted).
3. **14-day cooldown and a hard-failing SCA gate.** Given the monthly bump cadence, a two-week soak costs little responsiveness while covering essentially all yank/advisory windows; the SCA check fails the build rather than warning, so a known-vulnerable dependency cannot be merged unnoticed. Trade-off: a newly-disclosed advisory on an existing dependency can turn an unrelated PR's CI red until the dep is addressed — accepted as the point of the gate.
4. **Lock files were attempted and dropped.** The original plan committed a `packages.lock.json` per project and restored `--locked-mode` on CI to prove that shipped artifacts contain exactly the reviewed dependency bitstream. In practice `IsAotCompatible=true` (on `NetPace.Core` and `NetPace.Console`) pulls in the SDK-implicit `Microsoft.NET.ILLink.Tasks`, whose content hash is specific to the .NET SDK build; with `global.json` on `rollForward: latestFeature`, CI floats to a newer SDK feature band than the lock was generated on (e.g. 10.0.301 vs a dev machine's 10.0.109) and locked-mode restore fails NU1403 on a legitimate tree. Making lock files viable would require pinning the SDK feature band across dev and CI — a larger, ongoing toolchain-coupling cost (every SDK bump becomes a lock-regeneration step) judged disproportionate for a project this size. Rejected: pin the SDK to keep lock files. The tamper-evidence lock files would have added is left to the human review gate plus cooldown; revisit if the SDK-pinning cost ever becomes worthwhile.

**Known residual:** Without lock files, the resolved transitive graph is not content-pinned — a compromised-but-not-yet-advisory version that is past the cooldown window would still be ingestible, caught only by the human review gate. Explicitly pinning the floating Roslynator `[4.15.0, )` range and package-source-mapping remain follow-ups (the latter needs a second feed to matter). No SLSA/build-attestation provenance is in scope here.

**Date:** 2026-07-13
