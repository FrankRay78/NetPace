# Releasing NetPace

This document describes the release pipeline that runs when a semver tag is pushed: it builds and attaches downloadable artifacts to a GitHub Release, publishes `NetPace.Core` to NuGet when the library has changed, and submits an updated winget package for Windows users.

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

- `NetPace.Core.xml` — emitted because `NetPace.Core.csproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for NuGet consumers.
- `netpace.pdb` — emitted by the Windows linker for AOT builds regardless of `<DebugType>`.
- `netpace.dbg` — emitted by the Linux AOT toolchain when `StripSymbols` is on (the default for Release).

Archiving by name keeps the release contract immune to whatever the next toolchain version decides to emit alongside the binary — no per-extension scrub logic to maintain.

A "Verify archive contents" step asserts both invariants on every archive before upload as a safety net — exactly one entry, carrying the expected name — by comparing the archive listing against that name; non-zero exit aborts the release. It also catches a silent build failure that produced no binary. Asserting the name, not just the count, is what gates the lowercase command name on the case-insensitive Windows runners, where the archiver resolves a differently-cased binary and would otherwise ship it under that name.

`<DebugType>embedded</DebugType>` in both `NetPace.Console.csproj` and `NetPace.Core.csproj` is still set as belt-and-braces — it embeds portable PDBs inside the assembly so the publish directory has no managed `.pdb` side files even for local `dotnet publish` invocations.

End-user CLI binaries don't ship symbols — maintainers reproduce locally with their own debug build. If customer crash-dump symbolication is ever needed, push symbols to a workflow artefact rather than into the user-facing archive.

## Size-assertion contract

The `attach-to-release` job validates two relative size invariants before attaching any archive to the release:

1. For every RID: `framework-dependent < self-contained` (existing pre-feature check).
2. For Linux x64/arm64 and Windows x64/arm64: `aot < self-contained`.

Either invariant failing fails the entire release job; no archives are attached.

## winget package

NetPace is published to the Windows Package Manager community repository ([`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs)) as `FrankRay78.NetPace`, with the moniker `netpace` so that `winget install netpace` resolves. The identifier and the moniker are what users type, so both are contracts — changing either breaks every script that installs NetPace.

The package is a **zip manifest with a portable nested installer**: it wraps the existing `netpace-{ver}-win-{x64|arm64}-aot.zip` release archives, one installer node per architecture, with `NestedInstallerType: portable`, `RelativeFilePath: netpace.exe` and `PortableCommandAlias: netpace`. That alias is what puts `netpace` on the user's path without an installer and takes it off again on uninstall. There is no MSI or EXE setup program, and the self-contained and framework-dependent Windows builds are not offered through winget — the native AOT archive only.

**The submission is automated.** `.github/workflows/winget-submit.yml` runs `wingetcreate update` against the published release and opens a pull request on `microsoft/winget-pkgs`. Acceptance into that repository is reviewed by Microsoft's moderators and is outside this project's control; everything up to the pull request is hands-off.

| Decision | Why |
|----------|-----|
| Its own workflow, not a job in `release-binaries.yml` | Submitting to someone else's repository under the maintainer's personal credentials is a different function from building binaries, and must not be able to fail a release that built and published correctly. |
| Triggered by `release-binaries.yml` completing, not by the tag push | The submission reads installer URLs off the published release, and the archives do not exist until that workflow has attached them. |
| Not triggered by `release: published` | The GitHub Release is created first and the archives arrive afterwards, so that event fires before there is anything to read. |
| `windows-latest` runner | `wingetcreate` is a Windows executable and hashes each archive it is given. |
| `wingetcreate` taken at `latest`, not pinned | It tracks the manifest schema the community repository currently accepts; a pinned older build starts being rejected the next time that schema moves. |

**Installer URLs come off the release, never from string concatenation.** `scripts/winget-release-assets.sh` reads the release's own asset list and emits one `<url>|<architecture>` argument per installer, failing if either Windows AOT archive is absent and printing nothing at all when it does. A release missing an archive therefore stops before the submission, rather than producing a manifest whose download URL 404s for every user who installs it. The script also rejects a version carrying a `refs/tags/` prefix or a `v`, both of which would derive archive names that match nothing. Its behaviour is gated by `scripts/winget-release-assets.tests.sh`, which `shell-tests.yml` discovers and runs on every pull request.

Each URL carries its architecture explicitly, so the submission never depends on `wingetcreate` inferring `x64` from `arm64` by pattern-matching the archive name.

### One-off setup

Three things must exist before the automation can run. All three are one-time; none repeats per release.

1. **A fork of `microsoft/winget-pkgs`** on the maintainer's GitHub account. `wingetcreate` pushes its branch there and opens the pull request from it.
2. **A repository secret `WINGET_PKGS_TOKEN`** holding a GitHub personal access token with the `public_repo` scope, owned by the maintainer. It cannot be `GITHUB_TOKEN`, which has no access to another repository. The workflow fails on the missing secret before downloading or submitting anything, rather than skipping — a skip would report a release as published to winget when nothing was sent.
3. **The first manifest, submitted by hand.** `wingetcreate update` can only move an existing manifest forward; it cannot create one. Run `wingetcreate new` interactively on a Windows machine against both Windows AOT archive URLs of a published release and answer the nested-installer prompts with the values in *winget package* above, then submit. Once that pull request is merged, every subsequent release is automated.

The README.md and USER_GUIDE.md install instructions go live the moment they merge, so the first hand submission should follow promptly — until it is accepted, `winget install netpace` finds nothing.

### If a submission fails or is rejected

A failed submission **raises a GitHub issue** titled `winget submission failed for <version>`, linking the run. This exists because a `workflow_run`-triggered failure emails nobody: the run goes red in the Actions tab and otherwise passes silently. Treat the issue as the signal that `winget upgrade` will not reach that release.

To retry, run the **Submit winget Package** workflow manually from the Actions tab and give it the version to submit. It takes the same path as the automatic run, so a transient failure usually clears on a second attempt.

A rejection is different from a failure: the pull request was opened and the moderators declined it, so no issue is raised here and the feedback is on the pull request in `microsoft/winget-pkgs`. Address it there — amend the pull request rather than resubmitting, since a fresh submission for the same version collides with the open one. Common causes are a manifest-schema change (re-run the workflow, which picks up a newer `wingetcreate`) and validation failures against the installer itself; NetPace's Windows binaries are not code-signed, which is accepted by the community repository but can surface as a SmartScreen warning for users.

The GitHub Release and its archives are unaffected by any of this. A winget submission that never lands costs Windows users the one-line install for that version; it does not hold back the release.

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

## Source-build version

`src/Directory.Build.props` pins all four version properties — `Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion` — to `0.0.0`, so `netpace --version` from a local `dotnet build` or `dotnet run` reports `0.0.0`.

**Why**: `0.0.0` is below every release tag and matches none of them, so a build from source cannot be mistaken for a release.

**Released executables report their tag.** `release-binaries.yml` passes all four properties from the tag to each `dotnet publish`, overriding the placeholder.

**The `NetPace.Core` package reports its tag too.** `publish-nuget.yml` passes the same four properties to its Build step, and the `Verify packed library version` step fails the job if the packed library does not report the tag (see *NuGet package version*).

## SDK version pinning

The .NET SDK version is encoded in two places, both targeting **.NET 10 (LTS)**:

- **`global.json`** (repo root) pins `"version": "10.0.0"` with `"rollForward": "latestFeature"`, so local and CI builds resolve to the latest installed 10.0.x feature band — reproducible without hard-pinning a patch that may not be on every runner.
- **Each workflow** (`dotnet.yml`, `codeql.yml`, `publish-nuget.yml`, `release-binaries.yml`) sets `dotnet-version: 10.0.x` on `actions/setup-dotnet`, which installs a 10.0 SDK that `global.json` then honours.

When bumping the SDK major, update both places in lockstep.

## Release notes

Per-release "what changed" notes are **GitHub-auto-generated from the PRs merged since the last tag** — there is no `CHANGELOG.md` to maintain. The `NetPace.Core.csproj` `<PackageReleaseNotes>` property already points NuGet consumers to <https://github.com/FrankRay78/NetPace/releases>, so the auto-generated notes are the single source of truth for both CLI and library audiences. Edit the GitHub Release body manually only when a particular change deserves prose framing the auto-generated PR list can't supply.
