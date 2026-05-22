# Contract — `Profile` enum (public, NuGet-exposed)

**Namespace**: `NetPace.Core`
**File**: `src/NetPace.Core/Profile.cs`
**Stability**: pre-1.0 — additive growth only after this introduction.

## Declaration

```csharp
namespace NetPace.Core;

/// <summary>
/// Provider-agnostic vocabulary describing the intent of a speed-test run —
/// how much traffic to generate and how aggressively. Each provider's settings
/// record translates these labels into provider-specific values.
/// </summary>
public enum Profile
{
    /// <summary>IoT / 10 MB-month plans (≤ ~245 KB down + ~50 KB up per run).</summary>
    Tiny,

    /// <summary>Cellular / metered (≤ ~10 MiB down + ~2 MiB up per run).</summary>
    Small,

    /// <summary>Typical home broadband. Default profile (≤ ~100 MiB down + ~21 MiB up per run).</summary>
    Medium,

    /// <summary>Fibre / business (≤ ~1 GiB down + ~211 MiB up per run).</summary>
    Large,

    /// <summary>
    /// Inter-DC / 10 Gbps saturation (≤ ~10 GiB down + ~2 GiB up per run).
    /// Uses undocumented OoklaServer payloads (5000/6000/7000) which are not part
    /// of the historic Speedtest.net Flash-client array. May break on future
    /// OoklaServer releases — see docs/architecture/download-upload-size-controls.md.
    /// </summary>
    Mega
}
```

## Invariants

| Invariant | Enforcement |
|---|---|
| Member names are exactly `Tiny`, `Small`, `Medium`, `Large`, `Mega`. | Compile-time. |
| Ordinals are `0..4` (ascending traffic load). | Default-int backing; no explicit values assigned. |
| `Profile` declares no extension methods that reference any provider type. | Grep test under `NetPace.Core.Tests` (FR-002): assert no file containing `static.*Profile` in scope references the `Clients/Ookla/` namespace. |
| File location is `src/NetPace.Core/Profile.cs`, NOT under `Clients/`. | Structural test (FR-001): assert file path exists at that location. |
| Every member carries XML documentation. | Build with `TreatWarningsAsErrors=true` and CS1591 enabled; missing-doc warning fails the build. |

## Contract tests *(NetPace.Core.Tests/ProfileTests.cs)*

```csharp
// Structural — top-level placement
[Fact]
public void Profile_IsLocatedAtTopLevelOfNetPaceCore_NotUnderClients()
{
    // Reflection on the assembly to find the type, then assert its declaring
    // assembly's source-file path (via [CallerFilePath] or by string assertion
    // on the type's namespace).
    typeof(NetPace.Core.Profile).Namespace.Should().Be("NetPace.Core");
}

// Structural — no provider coupling
[Fact]
public void Profile_HasNoExtensionMethodReturningProviderType()
{
    // Reflect over the loaded NetPace.Core assembly; find any static method
    // whose first parameter is Profile and whose return type lives under
    // NetPace.Core.Clients.* — assert there are none.
}

// Membership
[Theory]
[InlineData(Profile.Tiny), InlineData(Profile.Small), InlineData(Profile.Medium),
 InlineData(Profile.Large), InlineData(Profile.Mega)]
public void Profile_AllExpectedMembers_AreDefined(Profile p) =>
    Enum.IsDefined(typeof(Profile), p).Should().BeTrue();
```

## Out of scope for this contract

- Per-provider mapping (lives on `OoklaSpeedtestSettings(Profile)` — see `ooklasettings-ctors.md`).
- Display strings for `--help` (the CLI Option's `Description` covers this).
- Localisation.
