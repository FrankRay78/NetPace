# Test Plan — Add `--profile` CLI switch (Tiny/Small/Medium/Large/Mega)

## Coverage summary

| User Story | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| Run NetPace on a constrained data plan without busting the cap | ✓ | ✓ | ⚠ | — | — | ✓ | 3 |
| Get a sensible default without thinking about it | ✓ | ✓ | ⚠ | — | — | ✓ | 3 |
| Saturate a 10 Gbps inter-DC link | ✓ | ✓ | — | ✓ | — | ✓ | 3 |
| Choose a profile, then override one cap | ✓ | ✓ | — | ✓ | — | — | 4 |
| Library consumer uses Profile from NetPace.Core | ✓ | ✓ | ✓ | — | — | — | 3 |

**Flags:**

- **Run NetPace on a constrained data plan**: no Error scenario in this story's labelled acceptance set. The `Edge Cases` section of spec.md describes invalid-`--profile` rejection (FR-013) and unknown-enum-value handling — consider adding a labelled `**Scenario:**` for it if you want it traceable from this test plan.
- **Get a sensible default without thinking about it**: no Error scenario. Same situation as above; both default-path stories rely on the well-formed CLI grammar working correctly.
- **Mega regression guard** has been classified as Boundary because it pins the high-end payload set against silent demotion — it functions as a guard rail rather than an error case, but it lives at the upper edge of the value space.

The two ⚠ flags reflect that error-class coverage for the CLI flag (unknown `--profile` value) is covered only at the requirements level (FR-013) and not in a labelled acceptance scenario. This is an acceptable specification trade-off — `System.CommandLine`'s default unknown-enum-value error is well-known behaviour — but if the team wants it tested explicitly, the spec should add a labelled scenario.

---

### User Story: Run NetPace on a constrained data plan without busting the cap

A user on a metered or IoT plan picks `--profile tiny` (or `--profile small`) and runs NetPace within their data budget.

#### Scenario: Tiny profile stays within IoT budget
- **WHEN** the CLI is invoked as `netpace --profile tiny` against a reachable Ookla server (or the local Docker OoklaServer) and the run completes successfully
- **THEN** total transferred bytes for the run are ≤ 1 MiB
- **AND** the bytes returned by the download phase fall within ±10 % of 245 KB (i.e. ~220 KB to ~270 KB)
- **AND** the bytes returned by the upload phase fall within ±10 % of 50 KB (i.e. ~45 KB to ~55 KB)
- **AND** the CLI exit code is `0`

#### Scenario: Small profile suits cellular
- **WHEN** the CLI is invoked as `netpace --profile small` against a reachable Ookla server and the run completes successfully
- **THEN** total transferred bytes for the run are ≤ 12 MiB (within ±10 % of the ~10 MiB down + ~2 MiB up target)
- **AND** the CLI exit code is `0`

#### Scenario: Profile is authoritative for per-request shape
- **WHEN** the CLI is invoked as `netpace --profile tiny` and the constructed `OoklaSpeedtestSettings` record is inspected after binding
- **THEN** `settings.DownloadTest.DownloadSizes` is exactly `[350]`
- **AND** `settings.DownloadTest.DownloadParallelTasks` is exactly `1`
- **AND** `settings.DownloadTest.DownloadSizeIterations` is exactly `1`
- **AND** no `DownloadSizes` entry larger than `350` is present (i.e. no full 4000-pixel JPEG request is generated)

---

### User Story: Get a sensible default without thinking about it

A user runs `netpace` with no flags and gets a Medium-profile run (~121 MiB total), down from the prior ~370 MiB.

#### Scenario: Omitted --profile defaults to Medium
- **WHEN** the CLI is invoked as `netpace` with no `--profile` flag and option binding completes
- **THEN** the `OoklaSpeedtestSettings` record built inside `Program.RunAsync` is equal (record equality) to `new OoklaSpeedtestSettings(Profile.Medium)`
- **AND** `settings.DownloadTest.DownloadSizes` is exactly `[1500, 2000, 3000, 3500, 4000]`
- **AND** `settings.DownloadTest.DownloadSizeMb` is exactly `100`
- **AND** `settings.UploadTest.UploadSizeMb` is exactly `25`

#### Scenario: Parameterless ctor chains to Medium
- **WHEN** library code calls `new OoklaSpeedtestSettings()` with no argument
- **THEN** the resulting record is equal (record equality) to `new OoklaSpeedtestSettings(Profile.Medium)`
- **AND** all 8 fields under `DownloadTest` and `UploadTest` are field-for-field identical to the Medium-profile values

#### Scenario: Default-run traffic drops vs pre-change baseline
- **WHEN** the CLI is invoked as `netpace` with no flags against a reachable Ookla server and the run completes successfully
- **THEN** the total transferred bytes reported for the run fall within ±10 % of 121 MiB
- **AND** the reported total is at least 65 % lower than the prior ~370 MiB default baseline (i.e. ≤ 130 MiB)
- **AND** the run's reported download and upload speed values are within the normal range produced by the prior default settings on the same link (no functional regression)

---

### User Story: Saturate a 10 Gbps inter-DC link

A power user runs `--profile mega` to push enough traffic to reach steady-state on 10 Gbps fibre.

#### Scenario: Mega uses bonus payloads
- **WHEN** `new OoklaSpeedtestSettings(Profile.Mega)` is constructed
- **THEN** `settings.DownloadTest.DownloadSizes` contains `5000`
- **AND** `settings.DownloadTest.DownloadSizes` contains `6000`
- **AND** `settings.DownloadTest.DownloadSizes` contains `7000`

#### Scenario: Mega's bonus-payload dependency is documented
- **WHEN** the assembly's XML documentation file (`NetPace.Core.xml`) is parsed for the `Profile.Mega` member
- **THEN** the doc text for `Profile.Mega` contains the substring `undocumented` (case-insensitive)
- **AND** the doc text references `5000`, `6000`, and `7000`
- **AND** the doc text references `docs/architecture/download-upload-size-controls.md`

#### Scenario: Mega regression guard
- **WHEN** the unit test asserting `new OoklaSpeedtestSettings(Profile.Mega).DownloadTest.DownloadSizes` runs against a future build
- **THEN** the assertion that the set contains `5000` passes
- **AND** the assertion that the set contains `6000` passes
- **AND** the assertion that the set contains `7000` passes
- **AND** if any of `5000`, `6000`, or `7000` is absent, the test fails with a message naming which value(s) are missing

---

### User Story: Choose a profile, then override one cap

A user wants Large's request shape but caps total download lower (e.g. `--profile large --downloadsize 100`).

#### Scenario: --downloadsize overrides only the cap, profile shape is preserved
- **WHEN** the CLI is invoked as `netpace --profile tiny --downloadsize 5` and option binding completes
- **THEN** `settings.DownloadTest.DownloadSizes` is exactly `[350]` (Tiny's per-request shape)
- **AND** `settings.DownloadTest.DownloadSizeIterations` is exactly `1` (Tiny's value)
- **AND** `settings.DownloadTest.DownloadParallelTasks` is exactly `1` (Tiny's value)
- **AND** `settings.DownloadTest.DownloadSizeMb` is exactly `5` (the override)

#### Scenario: --uploadsize overrides only the upload cap
- **WHEN** the CLI is invoked as `netpace --profile small --uploadsize 1` and option binding completes
- **THEN** `settings.UploadTest.UploadSizeIncrementKb` is exactly `100` (Small's per-request shape)
- **AND** `settings.UploadTest.UploadIncrements` is exactly `4` (Small's value)
- **AND** `settings.UploadTest.UploadSizeIterations` is exactly `2` (Small's value)
- **AND** `settings.UploadTest.UploadParallelTasks` is exactly `2` (Small's value)
- **AND** `settings.UploadTest.UploadSizeMb` is exactly `1` (the override)

#### Scenario: Override cap larger than natural transfer is a no-op backstop
- **WHEN** the CLI is invoked as `netpace --profile tiny --downloadsize 5000` against a reachable Ookla server and the run completes successfully
- **THEN** `settings.DownloadTest.DownloadSizeMb` on the constructed record is exactly `5000`
- **AND** the total transferred download bytes for the run fall within ±10 % of Tiny's natural 245 KB target (cap never triggers because Tiny completes well below 5000 MiB)
- **AND** the CLI exit code is `0`

#### Scenario: --no-download short-circuits regardless of profile
- **WHEN** the CLI is invoked as `netpace --no-download --profile large` against a reachable Ookla server
- **THEN** the CLI output reports zero bytes transferred for the download phase
- **AND** the upload phase still uses Large's per-request shape (parallel tasks = 16, increments = 8, increment Kb = 500)
- **AND** the CLI exit code is `0`

---

### User Story: Library consumer uses Profile from NetPace.Core

A NuGet consumer constructs settings from `Profile` directly without the CLI.

#### Scenario: Profile enum is provider-agnostic and at the root of NetPace.Core
- **WHEN** a developer reflects on `typeof(NetPace.Core.Profile)` in a unit test
- **THEN** `typeof(NetPace.Core.Profile).Namespace` is exactly `"NetPace.Core"` (no `Clients.*` suffix)
- **AND** no static method in the `NetPace.Core` assembly takes `Profile` as its first parameter and returns a type located under the `NetPace.Core.Clients` namespace
- **AND** the source file `src/NetPace.Core/Profile.cs` exists at that exact path (verified by file-existence check)
- **AND** no type named `OoklaSpeedtestSettingsExtensions` or `OoklaProfileExtensions` exists anywhere in the `NetPace.Core` assembly

#### Scenario: `with` expression composes cleanly on profile-built record
- **WHEN** the expression `var s = new OoklaSpeedtestSettings(Profile.Mega) with { UseProxy = true };` is evaluated
- **THEN** `s.DownloadTest.DownloadSizes` contains `5000`, `6000`, and `7000` (Mega's values are preserved)
- **AND** `s.UseProxy` is `true`
- **AND** `s.DownloadTest.DownloadParallelTasks` is exactly `32` (Mega's value)
- **AND** `s.UploadTest.UploadSizeIncrementKb` is exactly `1024` (Mega's value)

#### Scenario: Construct invalid profile throws
- **WHEN** the expression `new OoklaSpeedtestSettings((Profile)999)` is evaluated
- **THEN** an `ArgumentOutOfRangeException` is thrown
- **AND** the exception's `ParamName` property is exactly `"profile"`

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
