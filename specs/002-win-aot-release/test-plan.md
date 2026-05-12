# Test Plan — Windows Native AOT Release Artifacts

## Coverage summary

| User Story | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| Windows x64 user gets a fast-startup AOT NetPace | ✓ | — | — | ✓ | — | — | 4 |
| Windows ARM64 user gets a native AOT NetPace | ✓ | — | — | — | — | — | 3 |
| Release maintainer trusts the matrix and ships a clean tag | ✓ | — | ✓ | — | — | — | 5 |

**Flags:**
- *Windows x64 user gets a fast-startup AOT NetPace* — no Error scenario at the user-facing level. Failure modes (e.g. wrong-architecture binary refusal, SmartScreen prompt) are documented in spec.md §Edge Cases but the spec author intentionally keeps the user-facing acceptance set to happy-path + content + size + listing. Failures inside the build pipeline are covered by the maintainer-facing scenarios under User Story 3, so this is acceptable as a deliberate boundary not a gap.
- *Windows ARM64 user gets a native AOT NetPace* — only 3 scenarios and no Error coverage. Same reasoning as User Story 1: pipeline-level failures are exercised by the maintainer scenarios. Acceptable, but worth a re-look if any ARM64-specific runtime failure mode emerges in pre-release.
- No Recovery scenarios anywhere — for a release-pipeline feature, "recovery" would mean re-running a failed tag, which is outside the test scope (it's a maintainer workflow, not a verifiable system behaviour). Acceptable absence.

---

### User Story: Windows x64 user gets a fast-startup AOT NetPace
End-user verification of the new `win-x64-aot.zip` artefact on a clean Windows x64 host.

#### Scenario: Win-x64 AOT archive appears on tagged release
- **WHEN** a release is published for a semver tag `{tag}` (where `{tag}` matches the existing tag-extraction pattern `${GITHUB_REF#refs/tags/}`) after this feature ships, and the GitHub Release page for that tag is requested
- **THEN** an asset named `netpace-{tag}-win-x64-aot.zip` is present in the release-asset list
- **AND** assets `netpace-{tag}-win-x64-standalone.zip` and `netpace-{tag}-win-x64-net8.zip` remain present in the same release

#### Scenario: Win-x64 AOT binary runs with no dotnet runtime installed
- **WHEN** `netpace-{tag}-win-x64-aot.zip` is extracted on a Windows x64 host with no `.NET` runtime installed (verifiable by `where dotnet` returning non-zero or by a fresh image), and `NetPace.exe --version` is invoked
- **THEN** the process exits with code `0`
- **AND** stdout contains the version string `{tag}`

#### Scenario: Win-x64 AOT archive contains only the netpace.exe
- **WHEN** `netpace-{tag}-win-x64-aot.zip` is extracted to an empty directory and the directory listing is enumerated
- **THEN** the listing contains exactly one file
- **AND** that file is `NetPace.exe`
- **AND** no `.dll`, `.deps.json`, `.runtimeconfig.json`, or `.pdb` file is present

#### Scenario: Win-x64 AOT archive is smaller than win-x64 standalone
- **WHEN** `sizeof(netpace-{tag}-win-x64-aot.zip)` and `sizeof(netpace-{tag}-win-x64-standalone.zip)` are read from the release for the same tag
- **THEN** `sizeof(aot.zip) < sizeof(standalone.zip)` strictly (consistent with the Linux AOT size-assertion contract documented in `docs/RELEASING.md` §Size-assertion contract)
- **AND** the `attach-to-release` job's size-assertion step exited `0` for this tag

---

### User Story: Windows ARM64 user gets a native AOT NetPace
End-user verification of the new `win-arm64-aot.zip` artefact on a Windows-on-ARM host.

#### Scenario: Win-arm64 AOT archive appears on tagged release
- **WHEN** a release is published for a semver tag `{tag}` after this feature ships, and the GitHub Release page for that tag is requested
- **THEN** an asset named `netpace-{tag}-win-arm64-aot.zip` is present in the release-asset list

#### Scenario: Win-arm64 AOT binary runs natively on Windows-on-ARM
- **WHEN** `netpace-{tag}-win-arm64-aot.zip` is extracted on a Windows ARM64 host, and `NetPace.exe --version` and `NetPace.exe --help` are invoked in sequence
- **THEN** both processes exit with code `0`
- **AND** `--version` stdout contains the version string `{tag}`
- **AND** `--help` stdout contains the top-level command synopsis (any non-empty help text — the command is part of the existing CLI contract, not introduced by this feature)
- **AND** the binary's PE machine type is `IMAGE_FILE_MACHINE_ARM64` (`0xAA64`), confirming native ARM64 execution rather than x64 emulation. Verifiable via `dumpbin /HEADERS NetPace.exe` or equivalent PE-header inspection.

#### Scenario: Win-arm64 AOT archive contains only the netpace.exe
- **WHEN** `netpace-{tag}-win-arm64-aot.zip` is extracted to an empty directory and the directory listing is enumerated
- **THEN** the listing contains exactly one file
- **AND** that file is `NetPace.exe`
- **AND** no `.dll`, `.deps.json`, `.runtimeconfig.json`, or `.pdb` file is present

---

### User Story: Release maintainer trusts the matrix and ships a clean tag
Maintainer-facing pipeline guarantees: 16-archive count, no-regression on the existing 14, fail-fast on smoke and size-invariant violations, and self-documenting runner choices.

#### Scenario: Tagged release attaches exactly 16 archives
- **WHEN** a semver tag is pushed to `main` and the `release-binaries.yml` workflow run for that tag completes successfully
- **THEN** the GitHub Release for that tag has exactly 16 attached assets
- **AND** the asset names match the 16 entries listed in `specs/002-win-aot-release/contracts/release-matrix.md` §Outputs (case-sensitive, with the `{ver}` placeholder substituted to the pushed tag)
- **AND** no asset with any other name is attached

#### Scenario: Existing 14 archives are unchanged versus post-176 release
- **WHEN** the 14 pre-existing archives from a release tag of this feature are compared, file-by-file, against the same 14 archives from a comparable post-#176 release built from the same source state (i.e. the source state before this feature's diff was applied)
- **THEN** for every file inside each archive, the file list, file paths, and file sizes are identical
- **AND** the binary contents are identical except for the embedded `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` strings (which differ because the tag differs)
- **AND** no archive's filename has changed

#### Scenario: Failing Windows AOT smoke test halts release attach
- **GIVEN** a release tag where the Windows AOT smoke step (either `win-x64-aot` or `win-arm64-aot`) is deliberately forced to exit non-zero (e.g. by a temporary `exit 1` injected into the smoke step on a feature branch)
- **WHEN** the `release-binaries.yml` workflow runs for that tag
- **THEN** the failing matrix job ends with status `failure`
- **AND** the `attach-to-release` job does not run (status `skipped` due to the `needs: build-cross-platform` dependency)
- **AND** no asset is attached to the GitHub Release for that tag

#### Scenario: Windows AOT size invariant violation halts release attach
- **GIVEN** a release tag where the Windows AOT archive size is `≥` its `-standalone` counterpart (e.g. by a temporary archive-step modification that pads the AOT zip on a feature branch)
- **WHEN** the `release-binaries.yml` workflow runs for that tag
- **THEN** the `attach-to-release` job's size-assertion step exits non-zero
- **AND** the subsequent attach step does not run
- **AND** no asset is attached to the GitHub Release for that tag

#### Scenario: RELEASING.md documents win-arm64 runner choice
- **WHEN** the post-feature `docs/RELEASING.md` is read
- **THEN** the §Release matrix table contains a non-empty cell for the (`win-x64`, Native AOT) and (`win-arm64`, Native AOT) intersections
- **AND** the §Runner per RID table contains rows for `win-x64-aot` and `win-arm64-aot` with runner values `windows-latest` and `windows-11-arm` respectively
- **AND** the rationale for choosing `windows-11-arm` over a `windows-latest` cross-compile is captured in prose adjacent to that table (verifiable by string match: the rationale must reference both "cross-compile" and the native-runner reason — same shape as the existing `linux-arm64-aot` rationale)

---

## Implementation guidance

Every test method that implements a scenario in this plan MUST include a `// SCENARIO:`
comment whose value matches the `#### Scenario:` name above **exactly** — character for
character, including case, punctuation, and internal whitespace. Leading and trailing
whitespace on the scenario name is trimmed before comparison.

```csharp
[Fact]
public void Login_UnknownEmail_Returns401()
{
    // SCENARIO: Login rejected for unknown email

    // ...
}
```

`/speckit.testchecklist` validates these comments against the scenario names in this
file. A test without a matching `// SCENARIO:` comment is reported as untraced.
