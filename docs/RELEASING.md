# Releasing NetPace

This document describes the release pipeline that runs when a semver tag is pushed: it builds and attaches downloadable artifacts to a GitHub Release, publishes the `NetPace` .NET tool package to NuGet, and publishes the `NetPace.Core` library package when the library changed.

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
./netpace --version
./netpace --help
```

The AOT binary is named `netpace` (same as all other variants). On Windows the binary is `netpace.exe`; on Linux/macOS it is `netpace`.

## Archive-contents contract

Every release archive contains **exactly one entry** — the executable, named `netpace.exe` on Windows and `netpace` elsewhere.

The "Create archive" workflow step uses a **whitelist** approach: it archives the binary by name (`netpace.exe` on Windows, `netpace` elsewhere) rather than zipping/tarring the entire publish directory. All variants are designed to produce a single binary — `-p:PublishSingleFile=true` for non-AOT, native AOT for the AOT variants — but the publish directory also contains toolchain by-products that must not ship to end users:

- `NetPace.Core.xml` — emitted because `NetPace.Core.csproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for NuGet consumers. Removed from publish output by the `ExcludeDocXmlFromPublish` target in `NetPace.Console.csproj`, which the whitelist then makes moot for archives.
- `netpace.dbg` (Linux) / `netpace.pdb` (Windows) — the native symbol file the AOT toolchain emits beside the binary. `NetPace.Console.csproj` sets `<CopyOutputSymbolsToPublishDirectory>false</CopyOutputSymbolsToPublishDirectory>` for AOT publishes, so it no longer reaches the publish directory at all. That matters beyond the archives: the AOT publish directory is also what the RID-specific tool package is packed from, and no whitelist filters that — the symbol file is roughly twice the size of the binary, and it would otherwise make every native tool package about two and a half times its packed size. The AOT targets copy it in *after* `Publish` has run, so a publish-output filter cannot reach it; suppressing the copy is the only lever.

Archiving by name keeps the release contract immune to whatever the next toolchain version decides to emit alongside the binary — no per-extension scrub logic to maintain.

A "Verify archive contents" step asserts both invariants on every archive before upload as a safety net — exactly one entry, carrying the expected name — by comparing the archive listing against that name; non-zero exit aborts the release. It also catches a silent build failure that produced no binary. Asserting the name, not just the count, is what gates the lowercase command name on the case-insensitive Windows runners, where the archiver resolves a differently-cased binary and would otherwise ship it under that name.

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

The `IsAotCompatible=true` property on `NetPace.Core.csproj` causes `dotnet pack` to emit `[assembly: AssemblyMetadata("IsTrimmable", "True")]` into the packaged DLL — the standard .NET marker NuGet uses to surface AOT compatibility to consumers. The `publish-nuget.yml` workflow is unaffected by this property and consumes it transparently.

## NuGet package version

`publish-nuget.yml` takes the version from the tag and passes it to both steps that need it. The Build step receives `Version`, `AssemblyVersion`, `FileVersion` and `InformationalVersion`, because the compiler stamps those into `NetPace.Core.dll`. The Pack step runs with `--no-build`, so it can set only the package's own version.

**Version check**: before pushing, the `Verify packed library version` step unpacks the `.nupkg` and fails the job unless every `NetPace.Core.dll` inside reports the tag as its assembly version, file version and informational version. Nothing is pushed to nuget.org when it fails.

**Why**: with the version passed only to Pack, the package carried the tag while the library inside kept the placeholder from `src/Directory.Build.props`, and nothing checked it (#341).

## Conditional NuGet publish

`publish-nuget.yml` only packs and pushes `NetPace.Core` when `src/NetPace.Core/**` has changed between the current tag and the previous tag. On CLI-only tags (the common case — ~90% of releases), the workflow logs a skip message and exits successfully without invoking `dotnet pack` or `nuget push`.

**Why**: each tag would otherwise produce a new `NetPace.Core` version on nuget.org byte-equivalent to the previous one, eroding SemVer meaning for library consumers.

**Consequence**: `NetPace.Core` versions published to nuget.org may skip values (e.g. `0.5.0` → `0.7.0`) when intermediate tags were CLI-only. This is intentional — the published version always reflects a real Core change.

The GitHub Release / binary-attachment flow via `release-binaries.yml` is unaffected and ships every tag.

## .NET tool package

Every tag publishes the `NetPace` .NET tool package to NuGet, so `dotnet tool install -g NetPace` installs that release and `dotnet tool update -g NetPace` moves to it. The package set is seven packages per tag, all at the tag's version:

| Package | Contents | Packed by |
|---------|----------|-----------|
| `NetPace` | No binary — the wrapper package, carrying the command name and the list of RID packages the installer picks from | `publish-tool-package` job (`release-binaries.yml`) |
| `NetPace.win-x64` | Native AOT binary | `build-cross-platform` matrix, `win-x64-aot` entry |
| `NetPace.win-arm64` | Native AOT binary | `build-cross-platform` matrix, `win-arm64-aot` entry |
| `NetPace.linux-x64` | Native AOT binary | `build-cross-platform` matrix, `linux-x64-aot` entry |
| `NetPace.linux-arm64` | Native AOT binary | `build-cross-platform` matrix, `linux-arm64-aot` entry |
| `NetPace.osx-x64` | Framework-dependent build | `publish-tool-package` job |
| `NetPace.osx-arm64` | Framework-dependent build | `publish-tool-package` job |

**Why the packing is split.** A RID-specific tool package carrying a Native AOT binary can only be built on a runner matching that RID — the same constraint as the AOT archives (*Runner per RID*). So each AOT matrix entry packs its own RID package on its native runner and uploads it as a `tool-package-<rid>` artifact. The `publish-tool-package` job then adds the two macOS packages (framework-dependent, so they cross-pack from `ubuntu-latest`) and the wrapper, and pushes the set.

**Platform coverage.** The wrapper lists exactly those six RIDs, and the installer resolves the one matching the user's machine. A platform outside the list — Alpine-style Linux, for instance — cannot install the tool package at all: the wrapper carries no RID-agnostic fallback implementation, because packing one would mean a seventh, framework-dependent copy of NetPace in the package the installer reaches for *first*, on every platform. Those users download a release archive instead (USER_GUIDE.md says so).

**Release gates.** Four, all before `dotnet nuget push`:

1. `publish-tool-package` needs `build-cross-platform`, so a failed native build stops the push.
2. Each AOT matrix entry uploads its package by exact filename with `if-no-files-found: error`, so a pack that stopped producing one fails on the runner that packed it rather than two jobs later.
3. Every one of the four native packages is opened and its `tools/` entries read. A Native AOT pack puts the single native binary under `tools/any/<rid>/`; a framework-dependent pack puts `netpace.dll` and its dependencies under `tools/net10.0/<rid>/`. Both set `Runner="executable"` and both run fine on a runner that has the .NET runtime, so the install check below cannot tell them apart — without this gate, four framework-dependent packages would pass every check and ship to users promised a binary that needs no runtime. The entry count must be exactly two (the binary and its settings file), which also catches a native symbol file riding along (*Release archive contents*).
4. The packed set is installed from a local feed and asked its version: `dotnet tool install NetPace --version <tag> --source ./tool-feed --tool-path ./tool-check` then `./tool-check/netpace --version`, which must equal the tag. This exercises the chain end to end — the wrapper resolves a RID package, that package carries a runnable binary, and the binary reports the tag rather than the `0.0.0` source-build placeholder (see *Source-build version*). It covers `linux-x64` only, because that is the runner it runs on; gate 3 is what judges the other five.

**Push order.** The six RID packages are pushed first and the wrapper last. The wrapper carries no nuspec dependencies — it resolves its RID package at install time — so nuget.org will accept a wrapper pointing at packages that were never published. Pushing it last means a push that dies part-way leaves the version simply not installable, which a re-run fixes (`--skip-duplicate` makes that safe), rather than installable-and-broken for every user on the platform that did not make it.

**Only semver-shaped tags publish a tool package.** The workflow triggers on `*`, which includes the non-semver tags used to exercise the archive contract (`test-archive-whitelist-0.0.1`). The SDK cannot parse such a tag as a package version, so `dotnet pack` fails on it — and because `attach-to-release` needs `build-cross-platform`, an ungated pack failure inside the matrix would take the release archives down with a tag that used to publish them fine. A `check-tag` job classifies the tag and the pack, upload and publish steps are gated on it, so a test tag still builds and attaches every archive and simply publishes no package. Note that the gate is on the *shape* of the tag, not its intent: a prerelease-shaped tag such as `0.0.1-test1` is valid semver and does publish to NuGet, so keep test tags in the existing non-semver form.

**The archives do not wait for the tool package.** `attach-to-release` needs only `build-cross-platform`, so it runs alongside `publish-tool-package`: if a tool-package gate fails, the GitHub Release is still created with every binary attached, and only the NuGet push is missing. That is deliberate — a NuGet problem should not withhold the downloads — but it means "the gates stop the release" is true only of the tool package.

**One manual prerequisite.** `secrets.NUGET_API_KEY` must be scoped to push the `NetPace` and `NetPace.*` package IDs; before this it only ever pushed `NetPace.Core`. The `NetPace.*` prefix reservation on nuget.org is what keeps the six RID package IDs available, and the reservation must cover the bare `NetPace` ID as well as the prefix, or the wrapper push fails with a 403 on release day.

**No conditional skip.** The tool package publishes on every tag, unlike `NetPace.Core` (*Conditional NuGet publish*). The library's skip protects SemVer meaning for consumers who compile against it; a CLI user installing by version needs the release they asked for to exist. This is also why the tool package is published by `release-binaries.yml` rather than `publish-nuget.yml` — it ships with the binaries, on the same trigger and the same gates, not with the library.

**Local verification.** `scripts/tool-package.tests.sh` packs the console project at two versions, installs the result from a sandbox feed and runs it, then updates from the older version to the newer — covering `dotnet tool install`, `dotnet tool update` and `dnx`, the install-nothing path. It also opens all six RID packages and checks each carries the command for its platform, and that the wrapper advertises exactly the six the release publishes. It packs the framework-dependent form, because one machine cannot produce the four native binaries; the AOT pack and its shape are gated by the release workflow alone. The matrix runs on every pull request via `shell-tests.yml`.

## Source-build version

`src/Directory.Build.props` pins all four version properties — `Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion` — to `0.0.0`, so `netpace --version` from a local `dotnet build` or `dotnet run` reports `0.0.0`.

**Why**: `0.0.0` is below every release tag and matches none of them, so a build from source cannot be mistaken for a release.

**Released executables report their tag.** `release-binaries.yml` passes all four properties from the tag to each `dotnet publish`, overriding the placeholder.

**The `NetPace.Core` package reports its tag too.** `publish-nuget.yml` passes the same four properties to its Build step, and the `Verify packed library version` step fails the job if the packed library does not report the tag (see *NuGet package version*).

## SDK version pinning

The .NET SDK version is encoded in two places, both targeting **.NET 10 (LTS)**:

- **`global.json`** (repo root) pins `"version": "10.0.0"` with `"rollForward": "latestFeature"`, so local and CI builds resolve to the latest installed 10.0.x feature band — reproducible without hard-pinning a patch that may not be on every runner.
- **Each workflow** (`dotnet.yml`, `codeql.yml`, `publish-nuget.yml`, `release-binaries.yml`, `shell-tests.yml`) sets `dotnet-version: 10.0.x` on `actions/setup-dotnet`, which installs a 10.0 SDK that `global.json` then honours.

When bumping the SDK major, update both places in lockstep.

## Release notes

Per-release "what changed" notes are **GitHub-auto-generated from the PRs merged since the last tag** — there is no `CHANGELOG.md` to maintain. The `<PackageReleaseNotes>` property in both `NetPace.Core.csproj` and `NetPace.Console.csproj` already points NuGet consumers to <https://github.com/FrankRay78/NetPace/releases>, so the auto-generated notes are the single source of truth for both CLI and library audiences. Edit the GitHub Release body manually only when a particular change deserves prose framing the auto-generated PR list can't supply.
