# Feature Specification: Linux Native AOT Release Artifacts

**Feature Branch**: `001-linux-aot-release`
**Created**: 2026-05-01
**Status**: Draft
**Input**: User description: "https://github.com/FrankRay78/NetPace/issues/176 — Add Linux Native AOT release artifacts (linux-x64, linux-arm64) for IoT deployments"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Download a Native AOT binary for a Linux IoT device (Priority: P1)

An operator provisioning a Raspberry Pi, Jetson, or similar arm64/x64 Linux board wants to download a single, self-contained native binary of NetPace that runs without installing the .NET runtime, starts quickly on a resource-constrained device, and is materially smaller than the existing self-contained download.

**Why this priority**: This is the core value proposition of the feature — IoT/embedded deployment is the stated motivation, and without a downloadable AOT artifact for Linux RIDs, none of the rest of the feature delivers user value. Existing variants (`-standalone`, `-net8`) remain available unchanged, so this story can ship independently.

**Independent Test**: Tag a release, observe that two new archives (`netpace-{version}-linux-x64-aot.tar.gz` and `netpace-{version}-linux-arm64-aot.tar.gz`) appear on the GitHub Release page, download one onto a matching Linux device, extract it, and run `./netpace --version`. Success means the binary runs without a .NET runtime installed and reports the expected version.

**Acceptance Scenarios**:

1. **Scenario:** Tag push produces both new AOT archives  
   **Given** a tag is pushed to `main`, **When** the release pipeline completes, **Then** the resulting GitHub Release exposes 14 archives — the 12 existing variants unchanged plus `netpace-{tag}-linux-x64-aot.tar.gz` and `netpace-{tag}-linux-arm64-aot.tar.gz`.
2. **Scenario:** Smoke test --version exits zero on AOT archive  
   **Given** the `linux-x64-aot` archive is extracted on a Linux x64 host with no .NET runtime installed, **When** the user runs `./netpace --version`, **Then** the command exits with status `0` and prints the release version.
3. **Scenario:** Smoke test --help exits zero on AOT archive  
   **Given** the same archive, **When** the user runs `./netpace --help`, **Then** the command exits with status `0` and prints help output equivalent to the other Linux variants.
4. **Scenario:** Smoke test servers exits zero on AOT archive  
   **Given** the same archive, **When** the user runs `netpace servers`, **Then** the command performs an HTTPS request to the Ookla server endpoint, parses the XML response, and exits with status `0`.
5. **Scenario:** AOT archive contains no managed-runtime artefacts  
   **Given** the archive contents, **When** the user inspects them, **Then** the archive contains a single native ELF binary (no `.dll`, no embedded runtime, no `.deps.json`).

---

### User Story 2 - Existing Linux/Windows/macOS users see no change (Priority: P1)

Users (and downstream packaging — Homebrew, AUR, install scripts, internal docs) who currently rely on the `-standalone` or `-net8` archive names for any RID must continue to see those archives, with the same names and contents, after this change ships.

**Why this priority**: Backwards compatibility is non-negotiable. The issue explicitly preserves existing names; breaking them would invalidate consumer scripts and documentation. This story can be validated independently of any AOT functionality.

**Independent Test**: Tag a release before and after this change, diff the asset filenames and sizes for the 12 pre-existing variants. Each pre-existing archive name must still be present, and its contents must be functionally equivalent (same RID, same suffix, same archive format).

**Acceptance Scenarios**:

1. **Scenario:** All 12 pre-existing archive filenames present after change  
   **Given** a release tag, **When** the pipeline runs, **Then** all 12 pre-existing archive names (6 RIDs × `-standalone` and `-net8`) appear on the GitHub Release with their existing naming.
2. **Scenario:** Pre-existing matrix entries produce the same publish output  
   **Given** a user installs from a pre-existing `-standalone` archive on any supported RID, **When** they run NetPace, **Then** behaviour is unchanged from the prior release.
3. **Scenario:** publish-nuget.yml contents unchanged  
   **Given** the `publish-nuget.yml` workflow runs against the same tag, **When** it completes, **Then** a `NetPace.Core` NuGet package is published using the same workflow as before.

---

### User Story 3 - NuGet consumers see AOT-compatibility metadata on `NetPace.Core` (Priority: P2)

A developer building an AOT-published .NET application that depends on `NetPace.Core` wants the package to advertise AOT compatibility, so that AOT trim/IL analyzers in their consuming project don't surface false-positive warnings about `NetPace.Core` and so they get a clear signal that the library is supported in AOT scenarios.

**Why this priority**: Important for library consumers but not blocking for the IoT release goal in P1. Independently shippable: the metadata change is decoupled from the workflow changes for archives.

**Independent Test**: Run `dotnet pack` on `NetPace.Core` and inspect the resulting `.nupkg` metadata; confirm AOT compatibility is declared. In a separate AOT-published consumer project, reference the package and verify no AOT-related warnings originating from `NetPace.Core` are surfaced.

**Acceptance Scenarios**:

1. **Scenario:** Published NetPace.Core nupkg declares AOT compatibility  
   **Given** the `NetPace.Core` project is built, **When** it produces a NuGet package, **Then** the package metadata declares AOT compatibility.
2. **Scenario:** AOT consumer of NetPace.Core sees no AOT warnings from the package  
   **Given** an AOT-published consumer project references the new `NetPace.Core` package, **When** the consumer publishes with AOT enabled, **Then** no AOT trim or dynamic-code warnings originate from `NetPace.Core`.

---

### User Story 4 - Contributor understands and can extend the release matrix (Priority: P3)

A future contributor adding the next AOT target (Windows or macOS), or debugging a release failure, wants to find a single place that documents the release matrix, naming convention, and per-variant rationale rather than re-deriving it from workflow YAML.

**Why this priority**: Quality-of-life improvement for maintainers. Independently shippable as a docs-only change and not on the critical path for the IoT user.

**Independent Test**: A contributor unfamiliar with the project reads `docs/RELEASING.md` and can answer: how many archives a release produces, what each suffix means, which runners build which RID, and where to add a new RID — without opening the workflow YAML.

**Acceptance Scenarios** (documentation-only — covered by FR-016/FR-017; intentionally excluded from `test-plan.md`):

1. **Scenario:** Contributor identifies variants, runners and naming from RELEASING.md  
   **Given** a new contributor, **When** they read `docs/RELEASING.md`, **Then** they can list the variants, naming pattern, and rationale for each.
2. **Scenario:** README install table flags AOT as the IoT recommendation  
   **Given** the README install table, **When** a Linux/IoT user reads it, **Then** they can identify the AOT artefact as the recommended download for IoT/embedded deployments.

---

### Edge Cases

- **Reflection or dynamic-code path is exercised at runtime**: a binary may build successfully under AOT but crash on first use of a trimmed assembly. The smoke test (`--version`, `--help`, `servers`) must catch this before assets are attached to the release.
- **Locale-aware parsing on a host with no ICU**: with invariant globalization enabled for AOT, locale-specific parsing must continue to work for the supported feature surface (which is intentionally thin in NetPace).
- **AOT archive ends up larger than self-contained**: indicates trimming failed or AOT compilation produced unexpectedly large output; the size assertion (`aot < standalone`) must fail the build to surface this.
- **Network-required smoke test fails on a runner with restricted egress**: `netpace servers` requires HTTPS access to Ookla. Failure should fail the release job clearly, not silently skip.
- **Release pipeline runs on a non-tag push**: the new AOT matrix entries must follow the same trigger semantics as the existing entries — i.e., only run on the same release-triggering events.
- **A consumer pins to an old archive name**: the existing `-standalone` and `-net8` names continue to be produced unchanged; no rename in this scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The release pipeline MUST produce two additional release archives per tag: `netpace-{tag}-linux-x64-aot.tar.gz` and `netpace-{tag}-linux-arm64-aot.tar.gz`.
- **FR-002**: The release pipeline MUST continue to produce all 12 existing archive variants (6 RIDs × {self-contained, framework-dependent}) with unchanged filenames and contents.
- **FR-003**: Each AOT archive MUST contain a single native ELF binary, with no `.dll`, no embedded .NET runtime, and no `.deps.json` files.
- **FR-004**: The `linux-x64-aot` archive MUST be materially smaller than the `linux-x64-standalone` archive for the same tag; the `linux-arm64-aot` archive MUST be materially smaller than the `linux-arm64-standalone` archive for the same tag. The release job MUST fail if either size assertion fails.
- **FR-005**: The release pipeline MUST run a smoke test on each AOT archive on its native runner that executes `./netpace --version`, `./netpace --help`, and `netpace servers`; all three commands MUST exit with status `0` for the release job to succeed.
- **FR-006**: The `NetPace.Core` library MUST declare AOT compatibility, such that the published NuGet package signals AOT compatibility to consumers.
- **FR-007**: The `NetPace.Console` project MUST declare AOT compatibility for analyzer purposes.
- **FR-008**: A standard `dotnet build` of the full solution MUST emit zero warnings for AOT/trim warning codes `IL2026`, `IL2090`, `IL3050`, and `IL3056`.
- **FR-009**: An AOT publish (`dotnet publish src/NetPace.Console -c Release -r linux-x64 -p:PublishAot=true`) MUST complete with `IL2026`, `IL2090`, `IL3050`, and `IL3056` treated as errors and exit with status `0`.
- **FR-010**: The Ookla XML response parser used by `NetPace.Core` MUST be implemented without runtime reflection, so that AOT trimming does not break server discovery or response parsing. Parsing logic MUST be covered by unit tests in `NetPace.Core.Tests`.
- **FR-011**: The `NetPace.Console` formatting paths that previously depended on `Humanizer` MUST be re-implemented without `Humanizer`, preserving user-visible output.
- **FR-012**: The AOT publish MUST be configured for invariant globalization, producing a single binary with no ICU companion files; existing locale-aware parsing for the supported feature surface MUST continue to function.
- **FR-013**: AOT MUST be enabled via the `-p:PublishAot=true` MSBuild flag in the workflow only; non-AOT builds MUST be unaffected (no static `PublishAot` property in `csproj` files).
- **FR-014**: The two new AOT matrix entries MUST be expressed as explicit `matrix.include:` entries in the existing release workflow, each with its own runner (`ubuntu-latest` for x64, `ubuntu-24.04-arm` for arm64). The 12 pre-existing matrix entries MUST remain byte-identical.
- **FR-015**: AOT archives MUST omit the single-file publish flag, since native AOT already produces a single executable.
- **FR-016**: User-facing documentation MUST be updated:
  - `README.md` install table MUST list the new AOT artefacts and call out AOT as the recommended download for IoT/embedded deployments.
  - `USER_GUIDE.md` MUST include a short section on choosing between AOT, self-contained, and framework-dependent.
  - A new `docs/RELEASING.md` MUST document the release matrix, naming convention, and per-variant rationale.
  - Per-release "what changed" notes are GitHub-auto-generated from merged PRs (the `NetPace.Core.csproj` `<PackageReleaseNotes>` URL already points to GitHub Releases). No `CHANGELOG.md` is maintained.
- **FR-017**: A single Change Intent Record MUST accompany the implementation PR covering the `IsAotCompatible` public-API metadata change on `NetPace.Core`, the XML parser rewrite, and the release-pipeline extension.
- **FR-018**: The existing NuGet publish workflow MUST run unchanged on the same tag and publish a `NetPace.Core` package that declares AOT compatibility.
- **FR-019**: All existing tests in `NetPace.Core.Tests` and `NetPace.Console.Tests` MUST continue to pass.

### Key Entities

- **Release Archive**: a downloadable artefact attached to a GitHub Release, identified by RID and variant suffix (`-standalone`, `-net8`, `-aot`); for AOT, contains a single native ELF binary.
- **Release Matrix Entry**: a configuration row in the release workflow describing one (RID × variant × runner) build; the feature adds two new entries and leaves the existing 12 unchanged.
- **NuGet Package Metadata**: the metadata embedded in the `NetPace.Core` `.nupkg` that signals AOT compatibility to consumers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A release tag produces exactly 14 archives (12 pre-existing + 2 new AOT), 100% of the time across three consecutive releases, with no manual intervention required.
- **SC-002**: Each AOT archive's smoke test (`--version`, `--help`, `servers`) exits with status `0` on its native runner, every release.
- **SC-003**: For every release, both AOT archives are smaller than their `-standalone` counterparts for the same Linux RID; the release job fails automatically if not.
- **SC-004**: A user with no .NET runtime installed can extract a Linux AOT archive and successfully execute `netpace --version`, `netpace --help`, and `netpace servers` end-to-end.
- **SC-005**: A `dotnet build` of the full solution emits zero `IL2026`, `IL2090`, `IL3050`, or `IL3056` warnings.
- **SC-006**: 100% of pre-existing archive names remain present and functionally equivalent in the release after this feature ships (no rename, no removal).
- **SC-007**: A consumer of the `NetPace.Core` NuGet package, building with AOT enabled, sees zero AOT/trim warnings originating from `NetPace.Core`.
- **SC-008**: A new contributor can identify the variants, runners, and naming convention by reading `docs/RELEASING.md` alone, without opening the workflow YAML.

## Assumptions

- **GitHub-hosted ARM64 runners (`ubuntu-24.04-arm`) remain free for public repositories** at the time of implementation. If they cease to be free, this scope changes (cross-compilation or self-hosted runners would be needed) and the feature would need re-scoping.
- **Native runners are sufficient for smoke testing** — no QEMU/cross-arch emulation is required because the matrix uses native ARM64 and x64 runners.
- **`netpace servers` (HTTPS to Ookla) is reachable from GitHub-hosted runners** at release time. If egress is restricted on a given run, the release job is expected to fail loudly rather than silently skip.
- **Existing self-contained and framework-dependent variants remain in the matrix indefinitely** under this feature; deprecation of `-standalone` is explicitly deferred.
- **Windows AOT and macOS AOT are out of scope** and will be tracked as follow-up issues. Code signing/notarization is also out of scope.
- **The `XmlExtensions.cs` rewrite using `XDocument`/`XmlReader` is sufficient** to remove the only reflection-using code path in `NetPace.Core` that materially affects AOT; no broader audit is assumed necessary.
- **`Humanizer` is removable from `NetPace.Console`** by hand-rolling the small surface currently in use; this assumption was explicitly endorsed in the issue.
- **Invariant globalization is acceptable for the AOT publish** because NetPace's locale-aware parsing surface is already thin. Non-AOT variants are unaffected.
- **The CIR template at `docs/conventions/change-intent-records.md` is the right place to document the AOT-related public-API and pipeline changes**.
- **Tag format remains `{semver}` (not `v{semver}`)** — existing convention preserved; out of scope to change.
