# Test Plan — Linux Native AOT Release Artifacts

## Coverage summary

| Requirement | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| FR-001 (produce 2 new archives) | ✓ | — | ✓ | — | — | — | 2 |
| FR-002 (12 existing unchanged) | ✓ | ✓ | — | — | — | — | 2 |
| FR-003 (single ELF, no .dll/runtime/.deps.json) | ✓ | — | ✓ | — | — | — | 2 |
| FR-004 (aot < standalone per RID) | ✓ | — | ✓ | — | — | — | 2 |
| FR-005 (smoke test --version/--help/servers) | ✓ | — | ✓ | — | — | — | 4 |
| FR-006 (NetPace.Core NuGet AOT metadata) | ✓ | — | — | — | — | ✓ | 2 |
| FR-007 (NetPace.Console IsAotCompatible) | ✓ | — | — | — | — | — | 1 |
| FR-008 (zero IL warnings on dotnet build) | ✓ | — | ⚠ | — | — | — | 1 |
| FR-009 (AOT publish exits 0 with IL codes as errors) | ✓ | — | ✓ | — | — | — | 2 |
| FR-010 (XML parser AOT-safe + unit tested) | ✓ | ✓ | ✓ | ✓ | — | — | 6 |
| FR-011 (Humanizer-free formatting) | ✓ | — | — | ✓ | — | — | 4 |
| FR-012 (invariant globalization for AOT) | ✓ | ✓ | — | — | — | — | 2 |
| FR-013 (PublishAot via CLI flag only) | ✓ | — | — | — | — | — | 2 |
| FR-014 (matrix.include + 12 byte-identical) | ✓ | — | — | — | — | — | 2 |
| FR-015 (AOT omits single-file flag) | ✓ | — | — | — | — | — | 1 |
| FR-016 (documentation updated) | — | — | — | — | — | — | 0 (excluded) |
| FR-017 (CIR written) | — | — | — | — | — | — | 0 (excluded) |
| FR-018 (publish-nuget.yml unchanged) | ✓ | — | — | — | — | — | 2 |
| FR-019 (existing tests pass) | ✓ | — | — | — | — | — | 1 |

**Flags:**

- **FR-008**: `⚠` under Error class — warnings turning into errors is implicitly tested by FR-009 (AOT publish run), but there is no explicit scenario asserting that *injecting* a deliberate AOT-hazardous code path causes the build to fail. Considered acceptable given the analyzers themselves are vendor-tested; documented as an absence by design.
- **FR-016, FR-017**: excluded — documentation/CIR deliverables are reviewed via PR checklist, not test plan. Listed for completeness.
- **Spec acceptance scenarios lack `**Scenario:**` labels.** Constitution VIII requires labels in `spec.md` matching the `#### Scenario:` headers below. Reconcile before running `/speckit.analyze`.

---

### Requirement: FR-001

The release pipeline MUST produce two additional release archives per tag: `netpace-{tag}-linux-x64-aot.tar.gz` and `netpace-{tag}-linux-arm64-aot.tar.gz`.

#### Scenario: Tag push produces both new AOT archives
- **WHEN** a semver tag (e.g. `0.6.0`) is pushed to the repository and the `release-binaries.yml` workflow runs to completion
- **THEN** the GitHub Release for that tag has an asset named `netpace-0.6.0-linux-x64-aot.tar.gz`
- **AND** the GitHub Release has an asset named `netpace-0.6.0-linux-arm64-aot.tar.gz`

#### Scenario: AOT matrix entry failure prevents release
- **WHEN** the `linux-x64-aot` matrix entry's publish or smoke-test step exits non-zero during a tag-triggered run
- **THEN** the `attach-to-release` job does not run
- **AND** no `netpace-{tag}-linux-x64-aot.tar.gz` or `netpace-{tag}-linux-arm64-aot.tar.gz` asset is attached to the release

---

### Requirement: FR-002

The release pipeline MUST continue to produce all 12 existing archive variants (6 RIDs × {self-contained, framework-dependent}) with unchanged filenames and contents.

#### Scenario: All 12 pre-existing archive filenames present after change
- **WHEN** a tag is pushed and the workflow completes
- **THEN** the GitHub Release contains exactly these 12 assets: `netpace-{tag}-{rid}-{suffix}.{ext}` for `rid` ∈ `{win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64}`, `suffix` ∈ `{standalone, net8}`, with `ext` = `zip` for Windows RIDs and `tar.gz` otherwise
- **AND** the total release asset count is exactly 14 (12 pre-existing + 2 new AOT)

#### Scenario: Pre-existing matrix entries produce the same publish output
- **WHEN** the workflow's pre-existing matrix entries run on a tag
- **THEN** the `dotnet publish` invocation for each pre-existing entry passes the same flags (configuration, runtime, self-contained, output path, version metadata, `PublishSingleFile=true`) as before this feature shipped
- **AND** the per-entry archive (zip or tar.gz) for each pre-existing combination contains the same set of files (by filename) as the same combination's archive from the prior release built at the same SDK version

---

### Requirement: FR-003

Each AOT archive MUST contain a single native ELF binary, with no `.dll`, no embedded .NET runtime, and no `.deps.json` files.

#### Scenario: AOT archive contains a single ELF executable
- **WHEN** `netpace-{tag}-linux-x64-aot.tar.gz` is extracted to an empty directory
- **THEN** the directory contains exactly one regular file
- **AND** that file is named `netpace`
- **AND** running `file netpace` reports the file as an `ELF 64-bit LSB pie executable, x86-64`
- **AND** the file mode is executable (`+x`)

#### Scenario: AOT archive contains no managed-runtime artefacts
- **WHEN** `netpace-{tag}-linux-arm64-aot.tar.gz` is extracted to an empty directory
- **THEN** the directory contains zero files matching `*.dll`
- **AND** the directory contains zero files matching `*.deps.json`
- **AND** the directory contains zero files matching `*.runtimeconfig.json`
- **AND** the directory contains no subdirectory named `runtimes/`

---

### Requirement: FR-004

The `linux-x64-aot` archive MUST be materially smaller than the `linux-x64-standalone` archive for the same tag; the `linux-arm64-aot` archive MUST be materially smaller than the `linux-arm64-standalone` archive for the same tag. The release job MUST fail if either size assertion fails.

#### Scenario: AOT archive size strictly less than standalone per RID
- **WHEN** the `attach-to-release` job's size-validation step runs for a tag
- **THEN** the recorded size of `netpace-{tag}-linux-x64-aot.tar.gz` is strictly less than the recorded size of `netpace-{tag}-linux-x64-standalone.tar.gz`
- **AND** the recorded size of `netpace-{tag}-linux-arm64-aot.tar.gz` is strictly less than the recorded size of `netpace-{tag}-linux-arm64-standalone.tar.gz`

#### Scenario: Size assertion failure fails the release job
- **WHEN** for any release the AOT archive size for a Linux RID is greater than or equal to the corresponding standalone archive size
- **THEN** the size-validation step exits with a non-zero exit code
- **AND** the `attach-to-release` job's outcome is `failure`
- **AND** no archives are uploaded to the release for that run

---

### Requirement: FR-005

The release pipeline MUST run a smoke test on each AOT archive on its native runner that executes `./netpace --version`, `./netpace --help`, and `netpace servers`; all three commands MUST exit with status `0` for the release job to succeed.

#### Scenario: Smoke test --version exits zero on AOT archive
- **WHEN** the `linux-x64-aot` matrix entry's smoke-test step extracts the archive and runs `./netpace --version` on its `ubuntu-latest` runner
- **THEN** the process exits with status `0`
- **AND** standard output contains the tag version string (e.g. `0.6.0`)

#### Scenario: Smoke test --help exits zero on AOT archive
- **WHEN** the `linux-arm64-aot` matrix entry's smoke-test step extracts the archive and runs `./netpace --help` on its `ubuntu-24.04-arm` runner
- **THEN** the process exits with status `0`
- **AND** standard output contains a non-empty help message describing at least one subcommand

#### Scenario: Smoke test servers exits zero on AOT archive
- **WHEN** the `linux-x64-aot` matrix entry's smoke-test step extracts the archive and runs `./netpace servers` on its runner
- **THEN** the process exits with status `0`
- **AND** standard output contains at least one server entry parsed from the Ookla server-list response

#### Scenario: Smoke-test failure fails the matrix job
- **WHEN** any of `--version`, `--help`, or `servers` exits with a non-zero status during the smoke-test step
- **THEN** the matrix job's outcome is `failure`
- **AND** no archive is uploaded for that matrix entry
- **AND** the `attach-to-release` job is not run

---

### Requirement: FR-006

The `NetPace.Core` library MUST declare AOT compatibility, such that the published NuGet package signals AOT compatibility to consumers.

#### Scenario: Published NetPace.Core nupkg declares AOT compatibility
- **WHEN** `dotnet pack src/NetPace.Core/NetPace.Core.csproj -c Release` is run
- **THEN** the resulting `.nupkg`, when extracted, contains a `NetPace.Core.nuspec` whose metadata indicates `IsAotCompatible` is `true` (either via an explicit metadata element or via the inclusion of the AOT-compatibility marker NuGet emits when the property is set)

#### Scenario: AOT consumer of NetPace.Core sees no AOT warnings from the package
- **WHEN** a consumer .NET 8 project references the published `NetPace.Core` package and is built with `dotnet publish -p:PublishAot=true -warnaserror:IL2026,IL2090,IL3050,IL3056`
- **THEN** the publish completes with exit status `0`
- **AND** no warnings or errors with codes `IL2026`, `IL2090`, `IL3050`, or `IL3056` reference any type, member, or assembly belonging to `NetPace.Core`

---

### Requirement: FR-007

The `NetPace.Console` project MUST declare AOT compatibility for analyzer purposes.

#### Scenario: NetPace.Console csproj declares IsAotCompatible
- **WHEN** the contents of `src/NetPace.Console/NetPace.Console.csproj` are inspected as XML
- **THEN** there exists a `<PropertyGroup>` element containing a child `<IsAotCompatible>` element whose text value, when trimmed, equals `true`

---

### Requirement: FR-008

A standard `dotnet build` of the full solution MUST emit zero warnings for AOT/trim warning codes `IL2026`, `IL2090`, `IL3050`, and `IL3056`.

#### Scenario: Solution build emits zero AOT/trim warnings
- **WHEN** `dotnet build src/NetPace.sln -c Release` is run from a clean state
- **THEN** the process exits with status `0`
- **AND** the build output contains zero occurrences of `warning IL2026`
- **AND** the build output contains zero occurrences of `warning IL2090`
- **AND** the build output contains zero occurrences of `warning IL3050`
- **AND** the build output contains zero occurrences of `warning IL3056`

---

### Requirement: FR-009

An AOT publish (`dotnet publish src/NetPace.Console -c Release -r linux-x64 -p:PublishAot=true`) MUST complete with `IL2026`, `IL2090`, `IL3050`, and `IL3056` treated as errors and exit with status `0`.

#### Scenario: linux-x64 AOT publish completes successfully with IL codes as errors
- **WHEN** `dotnet publish src/NetPace.Console/NetPace.Console.csproj -c Release -r linux-x64 -p:PublishAot=true -p:InvariantGlobalization=true -warnaserror:IL2026,IL2090,IL3050,IL3056` is run
- **THEN** the process exits with status `0`
- **AND** the build output contains zero occurrences of `error IL2026`, `error IL2090`, `error IL3050`, or `error IL3056`

#### Scenario: AOT publish fails when an IL warning is introduced
- **WHEN** a deliberate `XmlSerializer.Deserialize<T>` call (which triggers `IL2026`) is added to `NetPace.Core` and the AOT publish from the previous scenario is re-run
- **THEN** the process exits with a non-zero status
- **AND** the build output contains at least one `error IL2026` entry referencing the introduced call site

---

### Requirement: FR-010

The Ookla XML response parser used by `NetPace.Core` MUST be implemented without runtime reflection, so that AOT trimming does not break server discovery or response parsing. Parsing logic MUST be covered by unit tests in `NetPace.Core.Tests`.

#### Scenario: Parser deserializes a representative Ookla server-list response
- **WHEN** `XmlExtensions.DeserializeFromXml<OoklaServerList>` is called with a captured Ookla `/speedtest-config.php` response containing 5 `<server>` entries
- **THEN** the returned `OoklaServerList` is non-null
- **AND** `OoklaServerList.Servers` has length `5`
- **AND** each `OoklaServer` has its `Id`, `Location`, `Sponsor`, `Url`, `Latitude`, and `Longitude` populated from the corresponding XML attribute values

#### Scenario: Parser populates optional attributes when present
- **WHEN** the parser is given XML in which a `<server>` element has both `country` and `host` attributes set
- **THEN** the resulting `OoklaServer.Country` equals the `country` attribute value
- **AND** `OoklaServer.Host` equals the `host` attribute value

#### Scenario: Parser leaves optional attributes null when absent
- **WHEN** the parser is given XML in which a `<server>` element omits both `country` and `host` attributes
- **THEN** the resulting `OoklaServer.Country` is `null`
- **AND** the resulting `OoklaServer.Host` is `null`

#### Scenario: Parser uses invariant culture for numeric attribute parsing
- **WHEN** the parser is given XML in which a `<server>` element has `lat="51.5074"` and `lon="-0.1278"` and the current thread's `CurrentCulture` is set to a comma-decimal locale (e.g. `de-DE`)
- **THEN** the resulting `OoklaServer.Latitude` equals `51.5074` (within `1e-9`)
- **AND** the resulting `OoklaServer.Longitude` equals `-0.1278` (within `1e-9`)

#### Scenario: Parser handles an empty servers element
- **WHEN** the parser is given XML containing `<settings><servers></servers></settings>`
- **THEN** the returned `OoklaServerList` is non-null
- **AND** `OoklaServerList.Servers` has length `0` (or is null and treated as empty by `OoklaSpeedtest.GetServersAsync`)

#### Scenario: Parser throws on malformed XML
- **WHEN** the parser is given a string that is not well-formed XML (e.g. unclosed `<settings>` element)
- **THEN** the call throws an `XmlException` (or an `InvalidOperationException` wrapping one)
- **AND** the exception message references the parse position

---

### Requirement: FR-011

The `NetPace.Console` formatting paths that previously depended on `Humanizer` MUST be re-implemented without `Humanizer`, preserving user-visible output.

#### Scenario: Replacement formatter produces "1 second" for one-second TimeSpan
- **WHEN** the replacement TimeSpan formatter is invoked with `TimeSpan.FromSeconds(1)`
- **THEN** the returned string equals `1 second`

#### Scenario: Replacement formatter pluralises for multi-second TimeSpan
- **WHEN** the replacement TimeSpan formatter is invoked with `TimeSpan.FromSeconds(7)`
- **THEN** the returned string equals `7 seconds`

#### Scenario: Replacement formatter rounds fractional seconds to whole seconds
- **WHEN** the replacement TimeSpan formatter is invoked with `TimeSpan.FromMilliseconds(2400)`
- **THEN** the returned string equals `2 seconds`

#### Scenario: Replacement formatter handles zero TimeSpan defensively
- **WHEN** the replacement TimeSpan formatter is invoked with `TimeSpan.Zero`
- **THEN** the returned string equals `0 seconds`

---

### Requirement: FR-012

The AOT publish MUST be configured for invariant globalization, producing a single binary with no ICU companion files; existing locale-aware parsing for the supported feature surface MUST continue to function.

#### Scenario: AOT archive contains no ICU data files
- **WHEN** `netpace-{tag}-linux-x64-aot.tar.gz` is extracted
- **THEN** the directory contains no files matching `icudt*.dat`
- **AND** the directory contains no files matching `libicu*.so*`

#### Scenario: AOT binary parses Ookla numeric attributes correctly under invariant globalization
- **WHEN** the AOT-built `netpace` binary is run with `LANG=C` and `./netpace servers`
- **THEN** the process exits with status `0`
- **AND** standard output displays at least one server with a parsed latitude/longitude value formatted with a `.` decimal separator

---

### Requirement: FR-013

AOT MUST be enabled via the `-p:PublishAot=true` MSBuild flag in the workflow only; non-AOT builds MUST be unaffected (no static `PublishAot` property in `csproj` files).

#### Scenario: csproj files contain no static PublishAot property
- **WHEN** the contents of `src/NetPace.Core/NetPace.Core.csproj` and `src/NetPace.Console/NetPace.Console.csproj` are inspected as XML
- **THEN** neither file contains any `<PublishAot>` element

#### Scenario: Workflow AOT entries pass PublishAot via MSBuild flag
- **WHEN** the contents of `.github/workflows/release-binaries.yml` are inspected
- **THEN** each `matrix.include` entry whose `deployment` value is `aot` includes the literal string `-p:PublishAot=true` in its publish command

---

### Requirement: FR-014

The two new AOT matrix entries MUST be expressed as explicit `matrix.include:` entries in the existing release workflow, each with its own runner (`ubuntu-latest` for x64, `ubuntu-24.04-arm` for arm64). The 12 pre-existing matrix entries MUST remain byte-identical.

#### Scenario: Workflow contains two matrix.include AOT entries with correct runners
- **WHEN** the parsed YAML of `.github/workflows/release-binaries.yml` is inspected
- **THEN** the matrix `include` list contains exactly two entries with `deployment: aot`
- **AND** the entry with `runtime: linux-x64` has `runs_on: ubuntu-latest`
- **AND** the entry with `runtime: linux-arm64` has `runs_on: ubuntu-24.04-arm`

#### Scenario: Pre-existing matrix grid is unchanged
- **WHEN** the pre-existing `matrix.runtime` list and `matrix.deployment` list in `.github/workflows/release-binaries.yml` are inspected
- **THEN** `matrix.runtime` equals exactly `[win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64]`
- **AND** `matrix.deployment` equals exactly `[self-contained, framework-dependent]`

---

### Requirement: FR-015

AOT archives MUST omit the single-file publish flag, since native AOT already produces a single executable.

#### Scenario: AOT publish command omits PublishSingleFile flag
- **WHEN** the `dotnet publish` command in each AOT matrix entry of `.github/workflows/release-binaries.yml` is inspected
- **THEN** the command does not contain the substring `-p:PublishSingleFile=true`

---

### Requirement: FR-018

The existing NuGet publish workflow MUST run unchanged on the same tag and publish a `NetPace.Core` package that declares AOT compatibility.

#### Scenario: publish-nuget.yml contents unchanged
- **WHEN** `.github/workflows/publish-nuget.yml` is compared to its pre-feature contents on the same source-control tag
- **THEN** the diff is empty

#### Scenario: Tag push publishes a NetPace.Core nupkg with AOT metadata
- **WHEN** a tag is pushed and `publish-nuget.yml` runs to completion
- **THEN** the workflow exits successfully
- **AND** a `NetPace.Core.{tag}.nupkg` is pushed to nuget.org
- **AND** the pushed package's metadata indicates AOT compatibility (per FR-006)

---

### Requirement: FR-019

All existing tests in `NetPace.Core.Tests` and `NetPace.Console.Tests` MUST continue to pass.

#### Scenario: Full test suite passes after the feature changes
- **WHEN** `dotnet test src/NetPace.sln -c Release` is run from a clean state on the post-feature branch
- **THEN** the process exits with status `0`
- **AND** the reported failed-test count equals zero across both `NetPace.Core.Tests` and `NetPace.Console.Tests`

---

## Implementation guidance

Every test method that implements a scenario in this plan MUST include a `// SCENARIO:`
comment whose value matches the `#### Scenario:` name above **exactly** — character for
character, including case and punctuation:

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
