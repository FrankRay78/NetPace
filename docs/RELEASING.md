# Releasing NetPace

This document describes the release pipeline that builds and attaches downloadable artifacts to a GitHub Release when a semver tag is pushed.

## Release matrix

Each tag produces one archive per cell of the matrix below — currently 16 (4 Native AOT plus 12 non-AOT).

| Runtime ID | Self-contained | Framework-dependent | Native AOT |
|------------|----------------|---------------------|------------|
| `win-x64` | `netpace-{ver}-win-x64-standalone.zip` | `netpace-{ver}-win-x64-net10.zip` | `netpace-{ver}-win-x64-aot.zip` |
| `win-arm64` | `netpace-{ver}-win-arm64-standalone.zip` | `netpace-{ver}-win-arm64-net10.zip` | `netpace-{ver}-win-arm64-aot.zip` |
| `linux-x64` | `netpace-{ver}-linux-x64-standalone.tar.gz` | `netpace-{ver}-linux-x64-net10.tar.gz` | `netpace-{ver}-linux-x64-aot.tar.gz` |
| `linux-arm64` | `netpace-{ver}-linux-arm64-standalone.tar.gz` | `netpace-{ver}-linux-arm64-net10.tar.gz` | `netpace-{ver}-linux-arm64-aot.tar.gz` |
| `osx-x64` | `netpace-{ver}-osx-x64-standalone.tar.gz` | `netpace-{ver}-osx-x64-net10.tar.gz` | _(out of scope)_ |
| `osx-arm64` | `netpace-{ver}-osx-arm64-standalone.tar.gz` | `netpace-{ver}-osx-arm64-net10.tar.gz` | _(out of scope)_ |

## Naming convention

`netpace-{version}-{runtime-id}-{variant}.{archive-format}`

- `version` — semver tag (e.g. `0.6.0`), no `v` prefix.
- `runtime-id` — .NET RID (`win-x64`, `linux-arm64`, …).
- `variant` — `standalone` (self-contained), `net10` (framework-dependent), or `aot` (native AOT).
- `archive-format` — `.zip` for Windows, `.tar.gz` everywhere else.

## Runner per RID

| RID | Runner | Rationale |
|-----|--------|-----------|
| All non-AOT entries | `ubuntu-latest` | Cross-compiles fine for non-AOT publishes; matches pre-feature behaviour. |
| `linux-x64-aot` | `ubuntu-latest` | Native x64 host — no cross-compile toolchain needed. |
| `linux-arm64-aot` | `ubuntu-24.04-arm` | Native ARM64 host — AOT cross-compilation is fragile, smoke test must run natively. GitHub-hosted ARM64 runners became free for public repos in January 2025. |
| `win-x64-aot` | `windows-latest` | Native x64 host — no cross-compile toolchain needed; `windows-latest` ships MSVC v143 and the Windows 11 SDK pre-installed. |
| `win-arm64-aot` | `windows-11-arm` | Native ARM64 host — AOT cross-compilation across architectures is fragile, smoke test must run natively. `windows-11-arm` runners became free for public repos in April 2025. |

Native AOT cannot be cross-compiled across operating systems — `dotnet publish` errors out with `Cross-OS native compilation is not supported`. Hence the per-RID native runners on both Linux and Windows.

## Smoke-test contract (AOT only)

Each AOT matrix entry runs two local-only commands against the freshly extracted archive on its native runner. Both MUST exit `0` for the matrix job to succeed; non-zero exit aborts the release.

```bash
./NetPace --version
./NetPace --help
```

The AOT binary is named `NetPace` (same as all other variants). On Windows the binary is `NetPace.exe`; on Linux/macOS it is `NetPace`.

## Archive-contents contract

Every release archive contains **exactly one entry** — the executable.

The "Create archive" workflow step uses a **whitelist** approach: it archives the binary by name (`NetPace.exe` on Windows, `NetPace` elsewhere) rather than zipping/tarring the entire publish directory. All variants are designed to produce a single binary — `-p:PublishSingleFile=true` for non-AOT, native AOT for the AOT variants — but the publish directory also contains toolchain by-products that must not ship to end users:

- `NetPace.Core.xml` — emitted because `NetPace.Core.csproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for NuGet consumers.
- `NetPace.pdb` — emitted by the Windows linker for AOT builds regardless of `<DebugType>`.
- `NetPace.dbg` — emitted by the Linux AOT toolchain when `StripSymbols` is on (the default for Release).

Archiving by name keeps the release contract immune to whatever the next toolchain version decides to emit alongside the binary — no per-extension scrub logic to maintain.

A "Verify archive contents" step asserts the one-entry invariant on every archive before upload as a safety net (it also catches a silent build failure that produced no binary); non-zero exit aborts the release.

`<DebugType>embedded</DebugType>` in both `NetPace.Console.csproj` and `NetPace.Core.csproj` is still set as belt-and-braces — it embeds portable PDBs inside the assembly so the publish directory has no managed `.pdb` side files even for local `dotnet publish` invocations.

End-user CLI binaries don't ship symbols — maintainers reproduce locally with their own debug build. If customer crash-dump symbolication is ever needed, push symbols to a workflow artefact rather than into the user-facing archive.

## Size-assertion contract

The `attach-to-release` job validates two relative size invariants before attaching any archive to the release:

1. For every RID: `framework-dependent < self-contained` (existing pre-feature check).
2. For Linux x64/arm64 and Windows x64/arm64: `aot < self-contained`.

Either invariant failing fails the entire release job; no archives are attached.

## Build-time AOT gating

- `<IsAotCompatible>true</IsAotCompatible>` is declared in both `src/NetPace.Core/NetPace.Core.csproj` and `src/NetPace.Console/NetPace.Console.csproj`. This activates the AOT/trim analyzers continuously during `dotnet build` and surfaces `IL2026`/`IL2090`/`IL3050`/`IL3056` warnings at compile time, not just at publish time.
- AOT publishes set `PublishAot=true`, which activates a conditional `<WarningsAsErrors>IL2026;IL2090;IL3050;IL3056</WarningsAsErrors>` block in `NetPace.Console.csproj`. The workflow also passes `-p:InvariantGlobalization=true`. `-p:PublishSingleFile=true` is **not** passed for AOT — native AOT already produces a single executable.
- `<PublishAot>` is **never** set as a static property in any csproj. AOT is opt-in via the workflow's `-p:PublishAot=true` flag; dev-machine builds remain non-AOT.

## NuGet metadata

The `IsAotCompatible=true` property on `NetPace.Core.csproj` causes the build to emit `[assembly: AssemblyMetadata("IsTrimmable", "True")]` into the packaged DLL — the standard .NET marker NuGet uses to surface AOT compatibility to consumers. The `publish-nuget.yml` workflow is unchanged by this feature and continues to consume the property transparently.

## Conditional NuGet publish

`publish-nuget.yml` only packs and pushes `NetPace.Core` when `src/NetPace.Core/**` has changed between the current tag and the previous tag. On CLI-only tags (the common case — ~90% of releases), the workflow logs a skip message and exits successfully without invoking `dotnet pack` or `nuget push`.

**Why**: each tag would otherwise produce a new `NetPace.Core` version on nuget.org byte-equivalent to the previous one, eroding SemVer meaning for library consumers.

**Consequence**: `NetPace.Core` versions published to nuget.org may skip values (e.g. `0.5.0` → `0.7.0`) when intermediate tags were CLI-only. This is intentional — the published version always reflects a real Core change.

The GitHub Release / binary-attachment flow via `release-binaries.yml` is unaffected and ships every tag.

## Source-build version

`src/Directory.Build.props` pins all four version properties — `Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion` — to `0.0.0`. `netpace --version` from a local `dotnet build` or `dotnet run` therefore reports `0.0.0`, via `AssemblyVersion` specifically: `Program.RunAsync` reads `Assembly.GetName().Version` and prints `Major.Minor.Build`, so that is the only one of the four the output reflects.

**Why**: `0.0.0` is below every release tag and matches none of them, so a build from source cannot be mistaken for a release. A plausible-looking placeholder has caused exactly that mistake before — the pin read `1.0.0` while the latest tag was `0.25.0`, and NetPace was discussed in issues and records as though already past its pre-1.0 stance (#331).

**The released executables carry the tag; the NuGet package's assembly does not.** The two release workflows differ, and the difference matters:

- `release-binaries.yml` passes all four properties to each `dotnet publish`, and none of those steps uses `--no-build`, so the compiler receives the tag. The attached archives hold one executable each, and the `NetPace.Core` code bundled inside them by `PublishSingleFile`/AOT carries the tag too, via the project reference. The separate `Build NetPace.Core.dll (library) with version` step also gets the properties, but its output is never archived or uploaded — nothing downstream consumes it, so it is not evidence of a tag-versioned Core asset. There is no standalone `NetPace.Core.dll` release asset.
- `publish-nuget.yml` does **not** reach the compiler with it. It builds first with no version properties, then packs with `--no-build`. Only the package version reaches the `.nuspec`; `AssemblyVersion`, `FileVersion` and `InformationalVersion` are compiler inputs, and the compiler never runs, so they have no effect anywhere. A published package is versioned by its tag while the `NetPace.Core.dll` inside it keeps this placeholder.

So the NuGet package is the only distribution of `NetPace.Core` as a standalone library, and no published `NetPace.Core` assembly reports its tag.

**What this changes, and for whom** (Constitution VII, *Pre-1.0 stance*, requires this stated in the issue or pull request; it is repeated here for the next reader, and does not discharge that obligation): `SignAssembly` is on, so `AssemblyVersion` is part of strong-name identity. Packages published while the four-property `1.0.0` pin was in place — from #46 until this branch — contain `NetPace.Core, Version=1.0.0.0`; earlier releases bumped the pin per release, so those carry the version their tag set. The next package published from this branch contains `Version=0.0.0.0`. .NET resolves by simple name and does not hard-enforce strong-name versions on load, so runtime breakage is unlikely — but a library consumer inspecting the installed assembly, or any diagnostic reporting `Assembly.GetName().Version`, now sees `0.0.0` for a real release, and MSBuild conflict resolution ranks a `0.0.0.0` assembly below any other copy on the graph. One piece of per-release identity does survive: SourceLink appends the commit, so the packaged `InformationalVersion` reads `0.0.0+<sha>` and still distinguishes one published package from another.

**Known issue, not yet raised**: the `publish-nuget.yml` gap pre-dates this placeholder, which only changes the wrong value it ships, so fixing the pipeline is separate work and needs its own issue — none covers it today. The fix is to give the Build step the same four properties or to drop `--no-build` from Pack; whichever is chosen, update this section with it.

**No gate proves a release reports its tag.** The AOT smoke test checks only the exit code of `--version`, never its text, so the placeholder is outside the smoke-test contract. The `Should_Display_Version` snapshot in `src/NetPace.Console.Tests/Expectations/` gates the *source-build* direction only, and only for `AssemblyVersion` in `NetPace.Console`: because the output is `Major.Minor.Build`, changing the first three fields of that value without updating the snapshot fails the suite, while a fourth-field change does not. `Version`, `FileVersion` and `InformationalVersion` are ungated and could drift — including back to a release-looking `1.0.0` — with the suite still green.

## SDK version pinning

The .NET SDK version is encoded in two places, both targeting **.NET 10 (LTS)**:

- **`global.json`** (repo root) pins `"version": "10.0.0"` with `"rollForward": "latestFeature"`, so local and CI builds resolve to the latest installed 10.0.x feature band — reproducible without hard-pinning a patch that may not be on every runner.
- **Each workflow** (`dotnet.yml`, `codeql.yml`, `publish-nuget.yml`, `release-binaries.yml`) sets `dotnet-version: 10.0.x` on `actions/setup-dotnet`, which installs a 10.0 SDK that `global.json` then honours.

When bumping the SDK major, update both places in lockstep.

## Release notes

Per-release "what changed" notes are **GitHub-auto-generated from the PRs merged since the last tag** — there is no `CHANGELOG.md` to maintain. The `NetPace.Core.csproj` `<PackageReleaseNotes>` property already points NuGet consumers to <https://github.com/FrankRay78/NetPace/releases>, so the auto-generated notes are the single source of truth for both CLI and library audiences. Edit the GitHub Release body manually only when a particular change deserves prose framing the auto-generated PR list can't supply.
