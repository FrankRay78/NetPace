# Hardening the Dependency Pipeline Against Supply-Chain Risk

**Intent:** Keep the automated dependency-update flow (monthly Dependabot PRs, human-gated, no auto-merge) but make ingesting a new package version *trustworthy* — so that "Frank reviewed and merged the PR" provably means "exactly that reviewed bitstream ships", a same-day compromised publish is never taken, and a known-vulnerable dependency is caught before release. Chosen over the alternative of stopping automation and bumping by hand.

**Behaviour:**
- Given: a `dotnet restore` on CI or a release runner, When: a transitive dependency has drifted from what was reviewed (different version or different content), Then: the restore fails rather than silently pulling the new bitstream (`--locked-mode` against a committed `packages.lock.json`).
- Given: Dependabot detects a newer package version, When: that version is younger than 14 days, Then: no PR is opened until the window elapses — the project never ingests a release on its publish day.
- Given: the build pipeline runs, When: any direct or transitive dependency has a known advisory, Then: CI fails the build (`dotnet list package --vulnerable --include-transitive`) and GitHub Dependabot security alerts flag it independently of the version-update PRs.

**Constraints:**
- The existing human gate stays: Dependabot opens PRs assigned to the maintainer; nothing auto-merges. Hardening adds assurance *around* that gate, it does not replace it.
- Lock files must be committed and regenerated deliberately (on an accepted bump), not on every local restore — CI restores are `--locked-mode`, developer restores are not.
- No new package feed is introduced, so package-source-mapping is out of scope (single implicit nuget.org feed = no dependency-confusion vector to close yet).
- CodeQL is SAST over our own code and does **not** cover dependency advisories; the SCA gate is additive, not a duplicate of it.

**Decisions:**
1. **Harden the automated flow, do not go manual.** Dependabot is not the attack surface — a malicious version reaches nuget.org regardless. Going manual trades away timely known-CVE patching (the common harm) to avoid a slice of the rare injection case that the human merge gate already covers. Rejected: disabling the chore workflow.
2. **Scope = lock files + cooldown + SCA (items 1–3); defer pin-Roslynator and source-mapping (4–5).** Committing `packages.lock.json` with `--locked-mode` freezes the floating Roslynator `[4.15.0, )` range as a side effect, so item 4's uncontrolled-ingest path closes without a separate change (an explicit pin remains desirable for honesty but is no longer a security gap). Item 5 needs a second feed to matter. Rejected: full 1–5 as disproportionate for a project this size.
3. **`--locked-mode` on CI/release restores only, not developer machines.** The tamper-evidence that matters is on the path that produces shipped artifacts. Forcing locked-mode locally would make routine dependency work friction-heavy for no security gain. Trade-off: a developer who forgets to regenerate the lock file on a bump gets a red CI, not a local failure — acceptable and self-correcting.
4. **Cooldown via Dependabot config over a bespoke delay mechanism.** Native `cooldown:` keeps the policy declarative in one file the maintainer already reviews. Rejected: custom scripting / Renovate migration (Renovate's `minimumReleaseAge` is equivalent but a larger tooling change than warranted).
5. **14-day cooldown and a hard-failing SCA gate.** Given the monthly bump cadence, a two-week soak costs little responsiveness while covering essentially all yank/advisory windows; the SCA check fails the build rather than warning, so a known-vulnerable dependency cannot be merged unnoticed. Trade-off: a newly-disclosed advisory on an existing dependency can turn an unrelated PR's CI red until the dep is addressed — accepted as the point of the gate.

**Known residual:** Lock files pin content but do not *audit* it — a version that is compromised yet not yet flagged by any advisory, and past the cooldown window, would still pass. The mitigation is the human review gate plus cooldown narrowing the window, not cryptographic provenance (no SLSA/build-attestation verification is in scope here). Package-source-mapping remains the follow-up if a private feed is ever added.

**Date:** 2026-07-13
