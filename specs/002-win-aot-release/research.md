# Phase 0 Research: Windows Native AOT Release Artifacts

**Feature**: 002-win-aot-release
**Date**: 2026-05-10

This document resolves the open technical questions before implementation begins. The spec already records most decisions as Confirmed Decisions inherited from issue #177; this file captures *why* those decisions stand and what was rejected.

## R1. Runner choice for `win-x64-aot`

- **Decision**: `runs-on: windows-latest`.
- **Rationale**: Native AOT compilation cannot be performed cross-OS — `dotnet publish -p:PublishAot=true` errors with `Cross-OS native compilation is not supported` when targeting `win-x64` from a non-Windows host. `windows-latest` (Windows Server 2022 at the time of writing) ships with MSVC v143 and the Windows 11 SDK pre-installed; no `setup-msbuild` or `setup-vs` step is required. Same image already used for the existing `win-x64` self-contained / framework-dependent matrix entries — no new image cost.
- **Alternatives considered**:
  - *Cross-compile from `ubuntu-latest`* — rejected by the SDK constraint above.
  - *Docker Windows container on `ubuntu-latest`* — feasible in principle but introduces a new runner image, slower cold-start, and no upside vs. running directly on `windows-latest`.

## R2. Runner choice for `win-arm64-aot`

- **Decision**: `runs-on: windows-11-arm`.
- **Rationale**: GitHub-hosted Windows-on-ARM runners (`windows-11-arm`) became GA in April 2025 and are free for public repositories — `FrankRay78/NetPace` qualifies. Native ARM64 host preserves the same-job smoke test and matches the runner-per-RID rationale `docs/RELEASING.md` already records for `linux-arm64-aot` (`ubuntu-24.04-arm`).
- **Alternatives considered**:
  - *Cross-compile from `windows-latest` (`-r win-arm64`)* — supported by the SDK but loses the ability to smoke-test the binary on its target architecture in the same job. Rejected by Confirmed Decisions in #177; same reasoning as for the Linux ARM64 case.
  - *Self-hosted ARM64 runner* — adds operational burden; unjustified while the GitHub-hosted runner is free for the repo.
- **Contingency**: If `windows-11-arm` becomes paywalled or is removed from the free tier, the cross-compile path is the documented fallback. Capturing that as a future issue rather than implementing it now.
- **Workflow visibility**: The `windows-11-arm` choice and its rationale will be captured as an inline comment on the matrix entry, per FR-011, so future contributors do not reach for cross-compile out of doubt.

## R3. `.pdb` exclusion from the release archive

- **Decision**: Exclude `.pdb` from the Windows AOT archive.
- **Rationale**: Matches the bare-binary posture of the Linux AOT archives (single `NetPace` executable, nothing else). Symbol publishing — whether via NuGet `.snupkg`, a separate symbol-artefact channel, or attaching `.pdb`s as their own release assets — is its own initiative and explicitly out of scope per #177 §3.
- **Mechanism**: The `dotnet publish` output for AOT on Windows produces `NetPace.exe` plus `NetPace.pdb` in the publish directory. The archive step must select only the `.exe` (or, equivalently, scrub the `.pdb` before archiving). The current archive step zips the entire publish directory unconditionally — see [`release-binaries.yml:99-107`](../../.github/workflows/release-binaries.yml#L99-L107) — so a one-line scrub (e.g. `rm *.pdb` on Windows runners when `deployment == aot`) is the simplest fit. Implementation detail to be finalised in tasks.
- **Acceptance**: Archive contents check (FR-003) will fail the release if any `.pdb` slips through.

## R4. Smoke-test shell on Windows runners

- **Decision**: `shell: bash` for the smoke step on Windows AOT entries.
- **Rationale**: Both `windows-latest` and `windows-11-arm` ship with Git Bash pre-installed. Reusing the same bash smoke logic that already runs on the Linux AOT entries (`./NetPace --version` / `./NetPace --help`) keeps the workflow simple — one branch in the smoke step, not two parallel implementations. The binary on Windows is `NetPace.exe`; bash on Windows runs it identically (`./NetPace.exe --version`). Confirmed in #177 Confirmed Decisions.
- **Alternatives considered**:
  - *PowerShell smoke* — would require a parallel implementation, no upside.
  - *cmd.exe smoke* — same objection.

## R5. Smoke-test depth

- **Decision**: Two commands only — `--version` and `--help`. No `servers`, no retry/timeout wrapper.
- **Rationale**: Matches the contract locked for Linux AOT in #176 / `docs/RELEASING.md` §Smoke-test contract. The two commands together exercise startup, AOT-trimmed reflection paths used by `System.CommandLine` parsing, and console rendering by Spectre.Console — sufficient to catch the AOT-specific failure modes (missing trim metadata, broken globalization, native-image init crash) without making the smoke step a network-dependent integration test. Broader smoke enhancements belong in a separate cross-cutting issue; doing them here would expand scope.
- **Alternatives considered**:
  - *Add `servers`* — calls Ookla servers list endpoint, network-dependent, flaky on shared runners. Rejected.
  - *Add a deliberate-failure check (`netpace.exe --bogus-flag`) to verify exit-code propagation* — unnecessary; covered by existing test suite.

## R6. Globalization mode for AOT

- **Decision**: `-p:InvariantGlobalization=true`, identical to Linux AOT.
- **Rationale**: Already wired into the AOT branch of the publish step (see [`release-binaries.yml:74-75`](../../.github/workflows/release-binaries.yml#L74-L75)). Windows divergence would be unjustified — none of NetPace's user-facing output requires culture-aware formatting that wouldn't already work under invariant culture. Avoids dragging the ICU data files into the AOT publish.

## R7. Archive format on Windows

- **Decision**: `.zip` (already produced by the existing `if [[ "${{ matrix.runtime }}" == win-* ]]` branch in the archive step). No change needed.
- **Rationale**: Convention; matches existing Windows release archives and `docs/RELEASING.md` §Naming convention.

## R8. Preserving the existing 14 archives byte-identical

- **Decision**: The two new matrix entries are added via `matrix.include:` and inherit the deployment shape (`publish_aot: true`, `publish_single_file: false`, `invariant_globalization: true`) that already exists for the Linux AOT entries. No edits to any of the six pre-existing `runtime` × `deployment` combinations.
- **Rationale**: `matrix.include:` extends the matrix without touching the base cross-product. Existing self-contained / framework-dependent jobs run with identical inputs and steps as before; their outputs are byte-identical for the same source state (modulo the version-string substitution from the tag, which is unchanged behaviour).
- **Verification**: After implementation, on a release-rehearsal tag, hash-compare the existing 14 archives against the comparable post-#176 release. Documented in quickstart.md.

## R9. Size-assertion contract extension

- **Decision**: Extend the existing size-assertion loop to cover Windows AOT — i.e. the `if [ "$variant" = "aot" ] && [[ "$runtime" != linux-* ]]; then continue; fi` guard at [`release-binaries.yml:158`](../../.github/workflows/release-binaries.yml#L158) becomes `if [ "$variant" = "aot" ] && [[ "$runtime" != linux-* && "$runtime" != win-* ]]; then continue; fi`, or equivalently is rewritten to allow-list the four AOT-bearing RIDs explicitly.
- **Rationale**: Without this change, the existing assertion would silently skip the new Windows AOT archives, defeating FR-004. Spec says the invariant must be enforced for the new RIDs, so the guard must be widened.
- **Implementation note**: Whether to widen the negation or rewrite as an explicit allow-list is a tactic for `/speckit.tasks` — both produce the same observable behaviour.

## R10. Trim/AOT warning behaviour on Windows-specific code paths

- **Decision**: Trust the existing `IsAotCompatible=true` static analysis to catch any Windows-specific reflection issue at `dotnet build` time, before the workflow even reaches the new matrix entries.
- **Rationale**: The clean-trim policy delivered by feature 001 enforces `WarningsAsErrors=IL2026;IL2090;IL3050;IL3056` whenever `PublishAot=true`. Both projects already declare `IsAotCompatible=true`, so the analyzers run on every `dotnet build` regardless of platform, surfacing platform-divergent reflection during ordinary CI long before release. No additional gating needed for Windows.
- **Risk**: If a Windows-only code path slipped past the analyzers (e.g. a `[DllImport]` to a Windows API used only at runtime under a `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` guard), it could surface as a runtime error in the smoke step. Mitigation: the smoke test is the second line of defence and will fail the release before attach.

## R11. Documentation scope

- **Decision**: Edit `README.md`, `docs/RELEASING.md`, and `USER_GUIDE.md`. Do **not** create or touch `CHANGELOG.md`.
- **Rationale**: The project has no `CHANGELOG.md`; per-release notes are GitHub-auto-generated from merged PRs (see `docs/RELEASING.md` §Release notes and the explicit Confirmed Decision in #177). Adding one now would be net-new infrastructure outside the feature's scope. Project memory at `.claude/memory/feedback_cli_feature_doc_scope.md` codifies this.

## Summary of resolved unknowns

All `NEEDS CLARIFICATION` items from Technical Context are resolved by R1–R11 above. The plan can proceed to Phase 1 design.
