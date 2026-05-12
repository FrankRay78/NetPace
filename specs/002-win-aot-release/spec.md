# Feature Specification: Windows Native AOT Release Artifacts

**Feature Branch**: `002-win-aot-release`
**Created**: 2026-05-10
**Status**: Draft
**Input**: GitHub issue [#177 — Add Windows Native AOT release artifacts (win-x64, win-arm64)](https://github.com/FrankRay78/NetPace/issues/177)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Windows x64 user gets a fast-startup AOT NetPace (Priority: P1)

A Windows desktop or server user on x64 hardware downloads a NetPace release, expects a small archive containing a single native executable that starts instantly with no .NET runtime install required and no JIT warm-up. They unzip, run `netpace.exe --version` to confirm the build, then run a speed test and see results print in the terminal.

**Why this priority**: x64 is overwhelmingly the most common Windows architecture for the NetPace audience (developers, sysadmins, hobbyists). Closing the parity gap with Linux AOT here delivers the bulk of the user-visible benefit and is a precondition for the ARM64 variant.

**Independent Test**: Cut a pre-release tag against a branch with only the `win-x64-aot` matrix entry added; confirm `netpace-{tag}-win-x64-aot.zip` appears on the release, contains a single `netpace.exe`, and runs end-to-end on a clean Windows x64 machine with no .NET runtime installed.

**Acceptance Scenarios**:

1. **Scenario: Win-x64 AOT archive appears on tagged release**
   **Given** a tagged release after this feature ships, **When** the user visits the GitHub release page, **Then** they see a `netpace-{tag}-win-x64-aot.zip` asset alongside the existing `win-x64-standalone.zip` and `win-x64-net8.zip` assets.
2. **Scenario: Win-x64 AOT binary runs with no dotnet runtime installed**
   **Given** the user has downloaded and extracted `netpace-{tag}-win-x64-aot.zip` on a Windows x64 machine with no .NET runtime installed, **When** they run `netpace.exe --version`, **Then** the command prints the version string and exits with code `0`.
3. **Scenario: Win-x64 AOT archive contains only the netpace.exe**
   **Given** the same extracted archive, **When** the user inspects its contents, **Then** the only file present is `netpace.exe` — no `.dll`, no `.deps.json`, no `.runtimeconfig.json`, no `.pdb`.
4. **Scenario: Win-x64 AOT archive is smaller than win-x64 standalone**
   **Given** the same release tag, **When** the user compares archive sizes, **Then** `netpace-{tag}-win-x64-aot.zip` is materially smaller than `netpace-{tag}-win-x64-standalone.zip`.

---

### User Story 2 - Windows ARM64 user gets a native AOT NetPace (Priority: P2)

A user on a Windows-on-ARM device (Surface Pro X, Snapdragon-based laptop, ARM64 IoT/edge box) downloads a NetPace release, expects a native ARM64 executable rather than an emulated x64 binary. They unzip, run `netpace.exe --version`, and see results without any x64 emulation overhead.

**Why this priority**: Smaller share of the Windows audience than x64 today, but parity matters for the IoT/edge story (the issue is labelled `embedded IOT`) and for the growing class of Windows-on-ARM laptops. Independently shippable from Story 1 — its absence does not block the x64 win.

**Independent Test**: Add only the `win-arm64-aot` matrix entry, push a pre-release tag, confirm `netpace-{tag}-win-arm64-aot.zip` is produced on a `windows-11-arm` runner, contains a single `netpace.exe`, and the same-job smoke test exits `0` natively (not via x64 emulation).

**Acceptance Scenarios**:

1. **Scenario: Win-arm64 AOT archive appears on tagged release**
   **Given** a tagged release after this feature ships, **When** the user visits the GitHub release page, **Then** they see a `netpace-{tag}-win-arm64-aot.zip` asset.
2. **Scenario: Win-arm64 AOT binary runs natively on Windows-on-ARM**
   **Given** the user has extracted `netpace-{tag}-win-arm64-aot.zip` on a Windows ARM64 machine, **When** they run `netpace.exe --version` and `netpace.exe --help`, **Then** both commands exit `0` and print the expected output.
3. **Scenario: Win-arm64 AOT archive contains only the netpace.exe**
   **Given** the archive, **When** the user inspects its contents, **Then** the only file present is `netpace.exe` (same single-file shape as the x64 AOT archive).

---

### User Story 3 - Release maintainer trusts the matrix and ships a clean tag (Priority: P3)

A maintainer pushes a semver tag to `main`. They expect the release pipeline to produce 16 archives in total — the existing 14 plus the two new Windows AOT variants — with no regression to the existing 14, and to halt the release before any artefact is attached if either new Windows AOT smoke test fails or a size invariant is violated.

**Why this priority**: Operational confidence rather than direct user value. Necessary to land Stories 1 and 2 safely, but not what the end user downloads. Captures the "no regression" and "fail-fast" guarantees that protect every prior release variant.

**Independent Test**: After Stories 1 and 2 are wired up, run the full release on a release-rehearsal tag and confirm: 16 archives produced, the existing 14 are byte-identical (ignoring tag-string differences) to a comparable post-#176 release, and a deliberately-broken AOT publish fails the matrix job before reaching the attach step.

**Acceptance Scenarios**:

1. **Scenario: Tagged release attaches exactly 16 archives**
   **Given** a fresh semver tag pushed after this feature ships, **When** the release workflow completes, **Then** exactly 16 archives are attached to the GitHub Release: the 14 from post-#176 plus `netpace-{tag}-win-x64-aot.zip` and `netpace-{tag}-win-arm64-aot.zip`.
2. **Scenario: Existing 14 archives are unchanged versus post-176 release**
   **Given** the same tag, **When** the maintainer compares the existing 14 archives against a comparable post-#176 release of the same source state, **Then** their contents are unchanged (no regression introduced by the matrix extension).
3. **Scenario: Failing Windows AOT smoke test halts release attach**
   **Given** a tag where the Windows AOT smoke test deliberately fails (e.g. a forced non-zero exit), **When** the workflow runs, **Then** the release attach step does not execute and no archives are published.
4. **Scenario: Windows AOT size invariant violation halts release attach**
   **Given** a tag where the Windows AOT archive is larger than its `-standalone` counterpart (size invariant violated), **When** the workflow runs, **Then** the release attach step does not execute.
5. **Scenario: RELEASING.md documents win-arm64 runner choice**
   **Given** the merged feature, **When** a future contributor reads `docs/RELEASING.md`, **Then** they find the Windows AOT rows in the release matrix, the `windows-latest` / `windows-11-arm` runner choices documented, and the rationale for not cross-compiling captured.

---

### Edge Cases

- **`windows-11-arm` runner unavailable / paywalled mid-release**: The `win-arm64-aot` matrix job fails fast; the release attach step does not execute; the maintainer can re-run the tag once availability returns. No automatic fallback to cross-compile (rejected by Confirmed Decisions in #177).
- **AOT publish surfaces a new IL2026/IL2090/IL3050/IL3056 warning on Windows but not Linux**: The AOT publish exits non-zero (warnings-as-errors), the matrix job fails, the release halts. Resolution is the same as for the Linux AOT path — fix the underlying trim/AOT issue in code.
- **End user on Windows x64 attempts to run the ARM64 AOT binary (or vice versa)**: Windows refuses to launch the binary with a platform-mismatch error. The user is expected to pick the archive matching their architecture from the release-asset list.
- **SmartScreen / AV warning on first download**: First-run warning is expected because Windows AOT binaries are unsigned (same posture as the existing `-standalone` Windows builds). Not remediated by this feature; user dismisses the warning. Code-signing is a separate, larger initiative.
- **Existing user on Windows continues to download `-standalone` or `-net8` archives**: They keep working unchanged. Both legacy variants remain in the release for compatibility; AOT is purely additive.
- **`.pdb` ends up inside the archive**: Treated as an acceptance-criterion violation — the archive contents check fails the release before attach.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The release pipeline MUST produce a `netpace-{version}-win-x64-aot.zip` archive whenever a semver tag is pushed.
- **FR-002**: The release pipeline MUST produce a `netpace-{version}-win-arm64-aot.zip` archive whenever a semver tag is pushed.
- **FR-003**: Each Windows AOT archive MUST contain exactly one file: a native `netpace.exe`. No `.dll`, `.deps.json`, `.runtimeconfig.json`, or `.pdb` companion files are permitted in the archive.
- **FR-004**: Each Windows AOT archive MUST be materially smaller than its `-standalone` counterpart for the same RID. The release pipeline MUST enforce this size invariant and halt the release if it is violated, mirroring the existing Linux AOT size check.
- **FR-005**: The release pipeline MUST run a smoke test on each Windows AOT archive on a runner that natively executes the target architecture (`windows-latest` for x64, `windows-11-arm` for ARM64). Cross-compiled smoke tests are not acceptable.
- **FR-006**: The Windows AOT smoke test MUST execute exactly two commands against the freshly extracted archive — `netpace.exe --version` and `netpace.exe --help` — and MUST require both to exit `0`. (Matches the smoke contract locked for Linux AOT in #176.)
- **FR-007**: Both Windows AOT publish steps MUST treat the AOT/trim warning codes already configured for AOT (currently `IL2026`, `IL2090`, `IL3050`, `IL3056`) as errors, identical to the Linux AOT path.
- **FR-008**: The existing 14 release archives (Windows / Linux / macOS self-contained and framework-dependent, plus Linux x64/arm64 AOT) MUST remain byte-identical to a comparable post-#176 release of the same source state. The Windows AOT addition MUST NOT regress any existing variant.
- **FR-009**: If either Windows AOT matrix job fails — at publish, smoke test, or size-assertion stage — the release attach step MUST NOT execute, mirroring the existing fail-fast posture.
- **FR-010**: User-facing documentation MUST be updated in lockstep with this feature:
  - `README.md` install table extended with `win-x64-aot.zip` and `win-arm64-aot.zip` rows.
  - `docs/RELEASING.md` release matrix and runner-per-RID table extended; the `windows-11-arm` choice for `win-arm64-aot` and the rejection of cross-compile MUST be documented inline.
  - `USER_GUIDE.md` AOT-vs-standalone-vs-framework-dependent note MUST mention that AOT is now also available for Windows.
  - `CHANGELOG.md` is **not** in scope — per-release notes are GitHub-auto-generated (per `docs/RELEASING.md` §Release notes).
- **FR-011**: `windows-11-arm` runner availability for the `FrankRay78/NetPace` public repository MUST be confirmed and that confirmation captured in the workflow (e.g. as a comment on the `win-arm64-aot` matrix entry) so future contributors do not doubt it or reach for cross-compile.
- **FR-012**: The release-asset count after this feature ships MUST be exactly 16 (the 14 from post-#176 plus the two new Windows AOT archives). No other variant is added or removed by this feature.

### Key Entities

- **Release archive**: A single `.zip` (Windows) or `.tar.gz` (Linux/macOS) attached to a GitHub Release, named per the convention in `docs/RELEASING.md`. The two new entities introduced here are `netpace-{version}-win-x64-aot.zip` and `netpace-{version}-win-arm64-aot.zip`.
- **Matrix entry**: A row of the release-pipeline build matrix that pairs a Runtime Identifier (RID), a deployment variant (`standalone` / `net8` / `aot`), and a runner image. This feature adds two such rows: `(win-x64, aot, windows-latest)` and `(win-arm64, aot, windows-11-arm)`.
- **Smoke test**: Two commands (`--version`, `--help`) executed against an extracted archive on its native runner, gating release attachment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Within one tagged release after this feature merges, the GitHub Release page shows exactly 16 attached archives, including `netpace-{tag}-win-x64-aot.zip` and `netpace-{tag}-win-arm64-aot.zip`.
- **SC-002**: A Windows x64 user with no .NET runtime installed can download `netpace-{tag}-win-x64-aot.zip`, extract it, and run `netpace.exe --version` successfully — measured by a green smoke-test step on every release tag.
- **SC-003**: Both Windows AOT archives are smaller than their `-standalone` counterparts (size invariant passes on every release tag — no manual override needed).
- **SC-004**: The existing 14 release archives remain identical in contents to the comparable post-#176 release for the same source state — verified by a manual diff on the first post-feature tag and by no churn in pre-existing archive consumers' install scripts.
- **SC-005**: A future contributor reading `docs/RELEASING.md` after this feature ships can identify, in under 60 seconds and without consulting any other document, why `windows-11-arm` is used for `win-arm64-aot` and why cross-compile is not used.
- **SC-006**: First-time-from-clean release run on a fresh semver tag completes in roughly the same wall-clock time as the comparable post-#176 release, plus the additional time for two Windows AOT matrix jobs in parallel — i.e. the feature does not serialize the matrix or otherwise inflate end-to-end release latency beyond the natural cost of the two new jobs.

## Assumptions

- **Issue #176 has merged before this work begins.** All cross-cutting AOT groundwork — `IsAotCompatible=true` on both projects, the clean-trim warning policy, the `XmlExtensions.cs` rewrite, the Humanizer resolution, the `InvariantGlobalization=true` AOT flag, the matrix-extension pattern, the size-assertion contract, the smoke-test contract — is in place and applies unchanged to Windows. (Confirmed by current `docs/RELEASING.md` showing 14 archives including the two Linux AOT variants.)
- **Windows runners are the right host for Windows AOT.** `windows-latest` for `win-x64-aot` and `windows-11-arm` for `win-arm64-aot`; no cross-compile from Linux or from `windows-latest` to ARM64. This decision is locked by Confirmed Decisions in #177 and mirrors the Linux runner-per-RID decision in `docs/RELEASING.md`.
- **`windows-11-arm` runners remain free for public repositories** for the foreseeable future. If GitHub paywalls or removes free ARM64 Windows runners, the `win-arm64-aot` job will fail at the runner-allocation stage; the contingency (cross-compile fallback) is explicitly out of scope and would be its own future issue.
- **`.pdb` is excluded from the release archive.** End-user crash debugging on bare-binary AOT downloads is acceptable; symbol distribution (NuGet `.snupkg` or a separate symbol channel) is a separate future initiative. Confirmed by Decision in #177 §3.
- **Unsigned Windows native binaries are acceptable for this release.** SmartScreen warnings on first download are expected; mitigation belongs with a separate code-signing initiative covering all release variants. Confirmed in #177 Out-of-scope.
- **`-standalone` and `-net8` Windows archives remain indefinitely.** This feature is purely additive; no existing variant is renamed, removed, or deprecated. Future deprecation of `-standalone` is a separate decision once Windows AOT proves itself across two or three releases.
- **macOS AOT is a separate, later issue.** Out of scope here; no macOS-runner work in this feature.
- **Smoke-test depth stays at the two-command contract.** No `servers` invocation, no retry/timeout wrapper. Broader smoke enhancements belong in a separate cross-cutting issue. Confirmed in #177 Confirmed Decisions.
- **Smoke-test shell is `bash`.** Git Bash is pre-installed on both Windows runners; the smoke step uses an `if`-branch for `unzip` vs `tar -xzf` and the binary name is `NetPace.exe` (capitalisation per case-sensitive convention) on Windows. Confirmed in #177 Confirmed Decisions.
