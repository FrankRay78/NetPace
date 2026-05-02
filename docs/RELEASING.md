# Releasing NetPace

This document describes the release pipeline that builds and attaches downloadable artifacts to a GitHub Release when a semver tag is pushed.

## Release matrix

Each tag produces **14 archives** — 12 pre-existing variants plus 2 new Linux Native AOT variants.

| Runtime ID | Self-contained | Framework-dependent | Native AOT |
|------------|----------------|---------------------|------------|
| `win-x64` | `netpace-{ver}-win-x64-standalone.zip` | `netpace-{ver}-win-x64-net8.zip` | _(out of scope)_ |
| `win-arm64` | `netpace-{ver}-win-arm64-standalone.zip` | `netpace-{ver}-win-arm64-net8.zip` | _(out of scope)_ |
| `linux-x64` | `netpace-{ver}-linux-x64-standalone.tar.gz` | `netpace-{ver}-linux-x64-net8.tar.gz` | `netpace-{ver}-linux-x64-aot.tar.gz` |
| `linux-arm64` | `netpace-{ver}-linux-arm64-standalone.tar.gz` | `netpace-{ver}-linux-arm64-net8.tar.gz` | `netpace-{ver}-linux-arm64-aot.tar.gz` |
| `osx-x64` | `netpace-{ver}-osx-x64-standalone.tar.gz` | `netpace-{ver}-osx-x64-net8.tar.gz` | _(out of scope)_ |
| `osx-arm64` | `netpace-{ver}-osx-arm64-standalone.tar.gz` | `netpace-{ver}-osx-arm64-net8.tar.gz` | _(out of scope)_ |

## Naming convention

`netpace-{version}-{runtime-id}-{variant}.{archive-format}`

- `version` — semver tag (e.g. `0.6.0`), no `v` prefix.
- `runtime-id` — .NET RID (`win-x64`, `linux-arm64`, …).
- `variant` — `standalone` (self-contained), `net8` (framework-dependent), or `aot` (native AOT).
- `archive-format` — `.zip` for Windows, `.tar.gz` everywhere else.

## Runner per RID

| RID | Runner | Rationale |
|-----|--------|-----------|
| All non-AOT entries | `ubuntu-latest` | Cross-compiles fine for non-AOT publishes; matches pre-feature behaviour. |
| `linux-x64-aot` | `ubuntu-latest` | Native x64 host — no cross-compile toolchain needed. |
| `linux-arm64-aot` | `ubuntu-24.04-arm` | Native ARM64 host — AOT cross-compilation is fragile, smoke test must run natively. GitHub-hosted ARM64 runners became free for public repos in January 2025. |

Native AOT cannot be cross-compiled across operating systems — `dotnet publish` errors out with `Cross-OS native compilation is not supported`. Hence the per-RID native runners.

## Smoke-test contract (AOT only)

Each AOT matrix entry runs three commands against the freshly extracted archive on its native runner. All three MUST exit `0` for the matrix job to succeed; non-zero exit aborts the release.

```bash
./netpace --version
./netpace --help
./netpace servers
```

`servers` exercises the AOT-safe XML parser (`XmlExtensions.DeserializeFromXml<OoklaServerList>`) end-to-end against the real Ookla wire format — catches AOT-only regressions that don't surface in normal builds.

## Size-assertion contract

The `attach-to-release` job validates two relative size invariants before attaching any archive to the release:

1. For every RID: `framework-dependent < self-contained` (existing pre-feature check).
2. For Linux x64 and arm64: `aot < self-contained`.

Either invariant failing fails the entire release job; no archives are attached.

## Build-time AOT gating

- `<IsAotCompatible>true</IsAotCompatible>` is declared in both `src/NetPace.Core/NetPace.Core.csproj` and `src/NetPace.Console/NetPace.Console.csproj`. This activates the AOT/trim analyzers continuously during `dotnet build` and surfaces `IL2026`/`IL2090`/`IL3050`/`IL3056` warnings at compile time, not just at publish time.
- AOT publishes additionally pass `-p:WarningsAsErrors=IL2026,IL2090,IL3050,IL3056` and `-p:InvariantGlobalization=true`. `-p:PublishSingleFile=true` is **not** passed for AOT — native AOT already produces a single executable.
- `<PublishAot>` is **never** set as a static property in any csproj. AOT is opt-in via the workflow's `-p:PublishAot=true` flag; dev-machine builds remain non-AOT.

## NuGet metadata

The `IsAotCompatible=true` property on `NetPace.Core.csproj` causes `dotnet pack` to emit `[assembly: AssemblyMetadata("IsTrimmable", "True")]` into the packaged DLL — the standard .NET 8 marker NuGet uses to surface AOT compatibility to consumers. The `publish-nuget.yml` workflow is unchanged by this feature and continues to consume the property transparently.

## Release notes

Per-release "what changed" notes are **GitHub-auto-generated from the PRs merged since the last tag** — there is no `CHANGELOG.md` to maintain. The `NetPace.Core.csproj` `<PackageReleaseNotes>` property already points NuGet consumers to <https://github.com/FrankRay78/NetPace/releases>, so the auto-generated notes are the single source of truth for both CLI and library audiences. Edit the GitHub Release body manually only when a particular change deserves prose framing the auto-generated PR list can't supply.
