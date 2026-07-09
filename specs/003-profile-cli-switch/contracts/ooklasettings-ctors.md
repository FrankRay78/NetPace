# Contract — `OoklaSpeedtestSettings` constructors

**Namespace**: `NetPace.Core.Clients.Ookla`
**File**: `src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs`
**Stability**: pre-1.0; both constructors are part of the public NuGet contract.

## Declaration

```csharp
public sealed record OoklaSpeedtestSettings
{
    public ServerDiscoverySettings ServerDiscovery { get; init; } = new();
    public LatencyTestSettings     LatencyTest     { get; init; } = new();
    public DownloadTestSettings    DownloadTest    { get; init; }
    public UploadTestSettings      UploadTest      { get; init; }
    public NetworkCredential?      ProxyCredential { get; init; }
    public Uri?                    ProxyAddress    { get; init; }
    public bool                    UseProxy        { get; init; }

    /// <summary>Builds settings for the default profile (<see cref="Profile.Medium"/>).</summary>
    public OoklaSpeedtestSettings() : this(Profile.Medium) { }

    /// <summary>Builds settings populated for the given profile.</summary>
    /// <param name="profile">The traffic-load profile to materialise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="profile"/> is not a defined <see cref="Profile"/> value.</exception>
    public OoklaSpeedtestSettings(Profile profile)
    {
        (DownloadTest, UploadTest) = profile switch
        {
            Profile.Tiny => (
                new DownloadTestSettings { DownloadSizes = new[] { 350 },                            DownloadSizeIterations = 1,  DownloadParallelTasks = 1,  DownloadSizeMb = 1     },
                new UploadTestSettings   { UploadSizeIncrementKb = 50,   UploadIncrements = 1,  UploadSizeIterations = 1,  UploadParallelTasks = 1,  UploadSizeMb = 1     }),

            Profile.Small => (
                new DownloadTestSettings { DownloadSizes = new[] { 1000, 1500 },                     DownloadSizeIterations = 2,  DownloadParallelTasks = 2,  DownloadSizeMb = 10    },
                new UploadTestSettings   { UploadSizeIncrementKb = 100,  UploadIncrements = 4,  UploadSizeIterations = 2,  UploadParallelTasks = 2,  UploadSizeMb = 2     }),

            Profile.Medium => (
                new DownloadTestSettings { DownloadSizes = new[] { 1500, 2000, 3000, 3500, 4000 },   DownloadSizeIterations = 2,  DownloadParallelTasks = 4,  DownloadSizeMb = 100   },
                new UploadTestSettings   { UploadSizeIncrementKb = 200,  UploadIncrements = 6,  UploadSizeIterations = 5,  UploadParallelTasks = 4,  UploadSizeMb = 25    }),

            Profile.Large => (
                new DownloadTestSettings { DownloadSizes = new[] { 2000, 2500, 3000, 3500, 4000 },   DownloadSizeIterations = 12, DownloadParallelTasks = 16, DownloadSizeMb = 1024  },
                new UploadTestSettings   { UploadSizeIncrementKb = 500,  UploadIncrements = 8,  UploadSizeIterations = 12, UploadParallelTasks = 16, UploadSizeMb = 256   }),

            Profile.Mega => (
                new DownloadTestSettings { DownloadSizes = new[] { 3000, 4000, 5000, 6000, 7000 },   DownloadSizeIterations = 40, DownloadParallelTasks = 32, DownloadSizeMb = 10240 },
                new UploadTestSettings   { UploadSizeIncrementKb = 1024, UploadIncrements = 16, UploadSizeIterations = 16, UploadParallelTasks = 32, UploadSizeMb = 2048  }),

            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
    }
}
```

## Behavioural contracts

| ID | Behaviour |
|---|---|
| **C-OS-1** | `new OoklaSpeedtestSettings()` is field-for-field equal (record equality) to `new OoklaSpeedtestSettings(Profile.Medium)`. |
| **C-OS-2** | `new OoklaSpeedtestSettings(profile).DownloadTest` and `.UploadTest` contain exactly the values listed in the table above, for every defined `Profile`. |
| **C-OS-3** | `new OoklaSpeedtestSettings((Profile)999)` throws `ArgumentOutOfRangeException` with `ParamName == "profile"`. |
| **C-OS-4** | `new OoklaSpeedtestSettings(Profile.Mega).DownloadTest.DownloadSizes` includes `5000`, `6000`, and `7000` (regression guard). |
| **C-OS-5** | `with`-expressions compose normally: `new OoklaSpeedtestSettings(Profile.Tiny) with { UseProxy = true }` produces a record with Tiny's `DownloadTest`/`UploadTest` and `UseProxy == true`. |
| **C-OS-6** | `with`-expressions on per-phase settings compose: `var s = new OoklaSpeedtestSettings(Profile.Tiny); s = s with { DownloadTest = s.DownloadTest with { DownloadSizeMb = 5 } };` preserves all other `DownloadTest` fields from Tiny and changes only `DownloadSizeMb`. |
| **C-OS-7** | `OoklaSpeedtestSettings` instance state has no `Profile` property — verified by reflection test. |
| **C-OS-8** | The codebase contains no `OoklaSpeedtestSettingsExtensions` or `OoklaProfileExtensions` class — verified by grep test (FR-006). |
