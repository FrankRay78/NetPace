# Contract — `--profile` CLI flag

**Binding library**: `System.CommandLine`
**Surface owner**: `src/NetPace.Console/Program.cs`
**Settings target**: `src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`

## Option declaration

```csharp
var profileOption = new Option<Profile>("--profile")
{
    Description = "Profile bundle of payload settings (Tiny | Small | Medium | Large | Mega).",
    DefaultValueFactory = _ => Profile.Medium
};
```

- No alias (no `-p`, no `--profile-name`).
- No custom parser — rely on `System.CommandLine`'s built-in case-insensitive enum binding.
- Default value source: `DefaultValueFactory` set to `Profile.Medium` (kept aligned with `OoklaSpeedtestSettings()` parameterless ctor's chain to `Profile.Medium`).

## Wiring

1. Add to the root command alongside `--unit-system`, `--unit-scale`, etc.
2. Bind onto `SpeedTestCommandSettings.Profile` (new `public Profile Profile { get; init; } = Profile.Medium;`).
3. `Program.RunAsync` constructs the settings record as:

```csharp
var settings = new OoklaSpeedtestSettings(commandSettings.Profile);

if (commandSettings.DownloadSizeMb is int dl)
    settings = settings with { DownloadTest = settings.DownloadTest with { DownloadSizeMb = dl } };

if (commandSettings.UploadSizeMb is int ul)
    settings = settings with { UploadTest = settings.UploadTest with { UploadSizeMb = ul } };

// proxy fields layered on top via the existing pattern…
```

## Behavioural contracts

| ID | Behaviour |
|---|---|
| **C-CLI-1** | `netpace` (no flags) ⇒ `settings.Equals(new OoklaSpeedtestSettings(Profile.Medium))`. |
| **C-CLI-2** | `netpace --profile tiny` / `--profile Tiny` / `--profile TINY` all parse to `Profile.Tiny` (case-insensitive). |
| **C-CLI-3** | `netpace --profile huge` exits non-zero with `System.CommandLine`'s default unknown-enum-value error message. |
| **C-CLI-4** | `netpace --profile tiny --downloadsize 5` ⇒ `settings.DownloadTest` has Tiny's `DownloadSizes`/iterations/parallel **and** `DownloadSizeMb == 5`. Profile remains authoritative for per-request shape; only the cap is overridden. |
| **C-CLI-5** | `netpace --profile small --uploadsize 1` ⇒ `settings.UploadTest` has Small's upload-increment fields **and** `UploadSizeMb == 1`. |
| **C-CLI-6** | `netpace --no-download --profile large` short-circuits the download phase regardless of profile (existing `--no-download` semantics unchanged). |
| **C-CLI-7** | `netpace --downloadsize 50` (no `--profile`) ⇒ Medium's per-request shape + `DownloadSizeMb == 50`. |
| **C-CLI-8** | `netpace --help` shows the `--profile` option with the `Description` text above, lists all five enum values, and shows `Medium` as the default. (Verified via VerifyXunit snapshot under `NetPace.Console.Tests/Expectations`.) |

## Help-output snapshot expectations

The `--help` snapshot (Verify) must include a line containing:

```
--profile <Tiny|Small|Medium|Large|Mega>  Profile bundle of payload settings (Tiny | Small | Medium | Large | Mega). [default: Medium]
```

Exact rendering depends on `System.CommandLine`'s default help formatter — accept whatever it produces, but the snapshot must update in lock-step with this change.

## Out of scope

- Custom error message for unknown values.
- Localised descriptions.
- Per-profile sub-help (e.g. `netpace --profile mega --help` showing Mega's table).
