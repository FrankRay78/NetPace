# Releasing NetPace

This document describes the release pipeline that runs when a semver tag is pushed: it builds and attaches downloadable artifacts to a GitHub Release, publishes the `NetPace` .NET tool package to NuGet, and publishes the `NetPace.Core` library package when the library changed.

## Release matrix

Each tag produces one archive per cell of the matrix below — currently 18 (6 Native AOT plus 12 non-AOT).

| Runtime ID | Self-contained | Framework-dependent | Native AOT |
|------------|----------------|---------------------|------------|
| `win-x64` | `netpace-{ver}-win-x64-standalone.zip` | `netpace-{ver}-win-x64-net10.zip` | `netpace-{ver}-win-x64-aot.zip` |
| `win-arm64` | `netpace-{ver}-win-arm64-standalone.zip` | `netpace-{ver}-win-arm64-net10.zip` | `netpace-{ver}-win-arm64-aot.zip` |
| `linux-x64` | `netpace-{ver}-linux-x64-standalone.tar.gz` | `netpace-{ver}-linux-x64-net10.tar.gz` | `netpace-{ver}-linux-x64-aot.tar.gz` |
| `linux-arm64` | `netpace-{ver}-linux-arm64-standalone.tar.gz` | `netpace-{ver}-linux-arm64-net10.tar.gz` | `netpace-{ver}-linux-arm64-aot.tar.gz` |
| `osx-x64` | `netpace-{ver}-osx-x64-standalone.tar.gz` | `netpace-{ver}-osx-x64-net10.tar.gz` | `netpace-{ver}-osx-x64-aot.tar.gz` |
| `osx-arm64` | `netpace-{ver}-osx-arm64-standalone.tar.gz` | `netpace-{ver}-osx-arm64-net10.tar.gz` | `netpace-{ver}-osx-arm64-aot.tar.gz` |

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
| `osx-x64-aot` | `macos-15-intel` | Native Intel host. Building `osx-x64` on an Apple silicon runner would leave the smoke test depending on Rosetta. `macos-15-intel` is GitHub's last x86_64 macOS image, supported until around August 2027 — see *When the Intel Mac runner retires*. |
| `osx-arm64-aot` | `macos-latest` | Native Apple silicon host. Hosted Mac runners are free for public repos. |

Native AOT cannot be cross-compiled across operating systems — `dotnet publish` errors out with `Cross-OS native compilation is not supported`. Hence the per-RID native runners on Linux, Windows and macOS. Splitting the two Mac RIDs across two runners is the smoke test's doing rather than the compiler's — see the table above.

The AOT jobs guard the runner they landed on: `RUNNER_OS` must match the RID's operating system, which catches a `runs_on` typo falling back to `ubuntu-latest`. The two macOS RIDs share an operating system, so they are additionally checked against `uname -m` — nothing else would notice the two Mac runners being swapped.

### When the Intel Mac runner retires

`macos-15-intel` has an announced end of life. When it goes, drop the `osx-x64` entries from the AOT matrix in `release-binaries.yml` and from `pack-native` in `publish-tool.yml`, and move `NetPace.osx-x64` back to a framework-dependent cross-pack in the `publish` job (the form it had before macOS AOT). Intel Mac users keep a working `dotnet tool install` and keep the `standalone` and `net10` archives; they lose only the `-aot` download and the runtime-free start.

## Smoke-test contract (AOT only)

Each AOT matrix entry runs two local-only commands against the freshly extracted archive on its native runner. Both MUST exit `0` for the matrix job to succeed; non-zero exit aborts the release.

```bash
./netpace --version
./netpace --help
```

The AOT binary is named `netpace` (same as all other variants). On Windows the binary is `netpace.exe`; on Linux/macOS it is `netpace`.

## macOS code signing

No macOS binary is signed with an Apple Developer ID or notarised. Apple silicon requires native code to be at least ad-hoc signed, which Apple's linker does by default, so the binaries run — but a `-aot` or `standalone` archive downloaded through a browser carries the quarantine flag and raises the usual Gatekeeper warning. Files laid down by `dotnet tool install` carry no quarantine flag, so the tool-package path is unaffected.

## Archive-contents contract

Every release archive contains **exactly one entry** — the executable, named `netpace.exe` on Windows and `netpace` elsewhere.

The "Create archive" workflow step uses a **whitelist** approach: it archives the binary by name (`netpace.exe` on Windows, `netpace` elsewhere) rather than zipping/tarring the entire publish directory. All variants are designed to produce a single binary — `-p:PublishSingleFile=true` for non-AOT, native AOT for the AOT variants — but the publish directory also contains toolchain by-products that must not ship to end users:

- `NetPace.Core.xml` — emitted because `NetPace.Core.csproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for NuGet consumers. Removed from publish output by the `ExcludeDocXmlFromPublish` target in `NetPace.Console.csproj`, which the whitelist then makes moot for archives.
- `netpace.dbg` (Linux) / `netpace.pdb` (Windows) / `netpace.dSYM` (macOS, a folder rather than a file) — the native symbols the AOT toolchain emits beside the binary. `NetPace.Console.csproj` sets `<CopyOutputSymbolsToPublishDirectory>false</CopyOutputSymbolsToPublishDirectory>` for AOT publishes, so it no longer reaches the publish directory at all. That matters beyond the archives: the AOT publish directory is also what the RID-specific tool package is packed from, and no whitelist filters that — they are roughly twice the size of the binary, and would otherwise make every native tool package about two and a half times its packed size. The AOT targets copy them in *after* `Publish` has run, so a publish-output filter cannot reach them; suppressing the copy is the only lever.

Archiving by name keeps the release contract immune to whatever the next toolchain version decides to emit alongside the binary — no per-extension scrub logic to maintain. The `tar` invocation sets `COPYFILE_DISABLE=1` for the same reason: Apple's `tar` otherwise serialises a file's extended attributes into a sibling `._netpace` member, which would add a second entry on the macOS runners alone. GNU `tar` ignores the variable.

A "Verify archive contents" step asserts both invariants on every archive before upload as a safety net — exactly one entry, carrying the expected name — by comparing the archive listing against that name; non-zero exit aborts the release. It also catches a silent build failure that produced no binary. Asserting the name, not just the count, is what gates the lowercase command name on the case-insensitive Windows runners, where the archiver resolves a differently-cased binary and would otherwise ship it under that name.

`<DebugType>embedded</DebugType>` in both `NetPace.Console.csproj` and `NetPace.Core.csproj` is still set as belt-and-braces — it embeds portable PDBs inside the assembly so the publish directory has no managed `.pdb` side files even for local `dotnet publish` invocations.

End-user CLI binaries don't ship symbols — maintainers reproduce locally with their own debug build. If customer crash-dump symbolication is ever needed, push symbols to a workflow artefact rather than into the user-facing archive.

## Size-assertion contract

The `attach-to-release` job validates two relative size invariants before attaching any archive to the release:

1. For every RID: `framework-dependent < self-contained` (existing pre-feature check).
2. For every RID: `aot < self-contained`.

Either invariant failing fails the entire release job; no archives are attached.

## Build-time AOT gating

- `<IsAotCompatible>true</IsAotCompatible>` is declared in both `src/NetPace.Core/NetPace.Core.csproj` and `src/NetPace.Console/NetPace.Console.csproj`. This activates the AOT/trim analyzers continuously during `dotnet build` and surfaces `IL2026`/`IL2090`/`IL3050`/`IL3056` warnings at compile time, not just at publish time.
- AOT publishes set `PublishAot=true`, which activates a conditional `<WarningsAsErrors>IL2026;IL2090;IL3050;IL3056</WarningsAsErrors>` block in `NetPace.Console.csproj`. The workflow also passes `-p:InvariantGlobalization=true`. `-p:PublishSingleFile=true` is **not** passed for AOT — native AOT already produces a single executable.
- `<PublishAot>` is **never** set as a static property in any csproj. AOT is opt-in via the workflow's `-p:PublishAot=true` flag; dev-machine builds remain non-AOT.

## NuGet metadata

The `IsAotCompatible=true` property on `NetPace.Core.csproj` causes `dotnet pack` to emit `[assembly: AssemblyMetadata("IsTrimmable", "True")]` into the packaged DLL — the standard .NET marker NuGet uses to surface AOT compatibility to consumers. The `publish-nuget.yml` workflow is unaffected by this property and consumes it transparently.

## NuGet package version

`publish-nuget.yml` takes the version from the tag and passes it to both steps that need it. The Build step receives `Version`, `AssemblyVersion`, `FileVersion` and `InformationalVersion`, because the compiler stamps those into `NetPace.Core.dll`. The Pack step runs with `--no-build`, so it can set only the package's own version.

**Version check**: before pushing, the `Verify packed library version` step unpacks the `.nupkg` and fails the job unless every `NetPace.Core.dll` inside reports the tag as its informational version and the tag's numeric part as its assembly and file versions (the same thing for a plain `X.Y.Z` tag — see *Prerelease tags*). Nothing is pushed to nuget.org when it fails.

**Why**: with the version passed only to Pack, the package carried the tag while the library inside kept the placeholder from `src/Directory.Build.props`, and nothing checked it (#341).

## NetPace.Core publishes on every release

Every release tag packs and pushes `NetPace.Core` at the tag's version, whether or not the library changed since the previous tag. Most releases change only the CLI, so most library versions are identical in content to the one before.

**Why**: the workflow used to publish only when `src/NetPace.Core/**` differed from the nearest earlier tag. That tag could be a release candidate, so a stable release tagged after an rc that carried the library's last change found no difference and never published the stable library. The diff also never saw `src/Directory.Build.props` or `src/Directory.Packages.props`, which shape the library too. Before 1.0 a version number makes no compatibility promise (Constitution VII, *Pre-1.0 stance*), so the skip protected nothing worth the risk of a release with no library.

The GitHub Release / binary-attachment flow via `release-binaries.yml` ships every release tag too.

## .NET tool package

Every semver tag publishes the `NetPace` .NET tool package to NuGet from `publish-tool.yml`, so `dotnet tool install -g netpace` installs that release and `dotnet tool update -g netpace` moves to it. NuGet package IDs are case-insensitive: the package is `NetPace` on nuget.org, the product name, while the docs give the lowercase form users type, matching the `netpace` command. The package set is seven packages per tag, all at the tag's version:

| Package | Contents | Packed by |
|---------|----------|-----------|
| `NetPace` | No binary — the wrapper package, carrying the command name and the list of RID packages the installer picks from | `publish` job |
| `NetPace.win-x64` | Native AOT binary | `pack-native` matrix, `win-x64` entry |
| `NetPace.win-arm64` | Native AOT binary | `pack-native` matrix, `win-arm64` entry |
| `NetPace.linux-x64` | Native AOT binary | `pack-native` matrix, `linux-x64` entry |
| `NetPace.linux-arm64` | Native AOT binary | `pack-native` matrix, `linux-arm64` entry |
| `NetPace.osx-x64` | Native AOT binary | `pack-native` matrix, `osx-x64` entry |
| `NetPace.osx-arm64` | Native AOT binary | `pack-native` matrix, `osx-arm64` entry |

**Why the packing is split.** A RID-specific tool package carrying a Native AOT binary can only be built on a runner matching that RID — the same constraint as the AOT archives (*Runner per RID*). So each `pack-native` matrix entry packs its own RID package on its native runner — the same runner the matching AOT archive is built on — and uploads it as a `tool-package-<rid>` artifact. The `publish` job then adds the wrapper, which carries no binary and so packs anywhere, and pushes the set.

**Platform coverage.** The wrapper lists exactly those six RIDs, and the installer resolves the one matching the user's machine. A platform outside the list — Alpine-style Linux, for instance — cannot install the tool package at all: the wrapper carries no RID-agnostic fallback implementation, because packing one would mean a seventh, framework-dependent copy of NetPace in the package the installer reaches for *first*, on every platform. Those users download a release archive instead (USER_GUIDE.md says so).

**Release gates.** Five, all before `dotnet nuget push`:

1. `publish` needs `pack-native`, so a failed native pack stops the push.
2. Each `pack-native` entry uploads its package by exact filename with `if-no-files-found: error`, so a pack that stopped producing one fails on the runner that packed it rather than a job later.
3. The packed set is installed from a local feed and asked its version: `dotnet tool install netpace --version <tag> --source ./tool-feed --tool-path ./tool-check` then `./tool-check/netpace --version`, which must equal the tag. This exercises the chain end to end — the wrapper resolves a RID package, that package carries a runnable binary, and the binary reports the tag rather than the `0.0.0` source-build placeholder (see *Source-build version*). It installs by the lowercase ID the docs give users, so the documented command is the one that is checked. It covers `linux-x64` only, because that is the runner it runs on; gates 4 and 5 judge the rest.
4. Each macOS entry of `pack-native` runs that same install-and-run check for itself, against a wrapper packed beside the RID package it has just built, on the same pass condition — the reported version must equal the tag. Before this change no macOS binary was built on a Mac at all, so the tool workflow never executed one. The `publish` job's check runs on Linux, so `win-x64`, `win-arm64` and `linux-arm64` still have that same unexecuted gap.
5. Last, immediately before the push, every one of the six RID packages in the feed is opened and its `tools/` entries read — so it judges exactly what is pushed, not what was downloaded, and nothing packed later in the job (the wrapper pack writes to the same feed) can replace an AOT package unseen. A Native AOT pack puts the single native binary under `tools/any/<rid>/`; a framework-dependent pack puts `netpace.dll` and its dependencies under `tools/net10.0/<rid>/`. Both set `Runner="executable"` and both run fine on a runner that has the .NET runtime, so the install checks above cannot tell them apart — without this gate, six framework-dependent packages would pass every check and ship to users promised a binary that needs no runtime. The entry count must be exactly two (the binary and its settings file), which also catches native symbols riding along — including the macOS `netpace.dSYM` folder (*Archive-contents contract*).

**Push order.** The six RID packages are pushed first and the wrapper last. The wrapper carries no nuspec dependencies — it resolves its RID package at install time — so nuget.org will accept a wrapper pointing at packages that were never published. Pushing it last means a push that dies part-way leaves the version simply not installable, which re-running the failed `publish-tool.yml` run fixes (`--skip-duplicate` makes that safe) — the recovery for any failed tool publish, since the tag and its archives are already out, rather than installable-and-broken for every user on the platform that did not make it.

**The archives and the tool package cannot fail each other.** They are separate workflows on the same tag, and no job in one needs a job in the other. A failed native pack or tool-package gate leaves the GitHub Release created with every binary attached and only the NuGet push missing; a failed archive build leaves the tool package unaffected. The cost is that each native RID is compiled once by each workflow — no more than packing inside the archive matrix cost, because a tool package is packed from the project rather than from the archive's binary — and the six native runners are listed in both files, which must change together.

**Authentication.** The push uses trusted publishing, like the library's — see *Trusted publishing*.

**Every tag publishes.** Like `NetPace.Core` (*NetPace.Core publishes on every release*), the tool package publishes on every release tag, because a user installing by version needs the release they asked for to exist.

**Local verification.** `scripts/tool-package.tests.sh` packs the console project at two versions, installs the result from a sandbox feed and runs it, then updates from the older version to the newer — covering `dotnet tool install`, `dotnet tool update` and `dnx`, the install-nothing path. It also opens all six RID packages and checks each carries the command for its platform, and that the wrapper advertises exactly the six the release publishes. It packs the framework-dependent form, because one machine cannot produce the six native binaries; the AOT pack and its shape are gated by `publish-tool.yml` alone. The matrix runs on every pull request via `shell-tests.yml`.

## Trusted publishing

Both NuGet publishes — `publish-nuget.yml` for `NetPace.Core` and `publish-tool.yml` for the tool package — authenticate by trusted publishing, so no long-lived API key is stored in the repository. Immediately before its push, each publish job runs `NuGet/login`, which exchanges the job's GitHub OIDC token for an API key that expires within the hour; the push uses that key and nothing else.

nuget.org issues the key only to a workflow it has a policy for. A policy names the repository owner, the repository and **one workflow file**, so there are two, one per publishing workflow:

| nuget.org trusted publishing policy | Repository | Workflow file | Pushes |
|-------------------------------------|------------|---------------|--------|
| Publish NetPace Core | `FrankRay78/NetPace` | `publish-nuget.yml` | `NetPace.Core` |
| Publish NetPace tool | `FrankRay78/NetPace` | `publish-tool.yml` | `NetPace` and the six `NetPace.<rid>` packages |

**What the repository holds.** One secret, `NUGET_USER`: the nuget.org profile name (not the email address) that owns the policies, which `NuGet/login` sends with the exchange. Each publish job declares `id-token: write`, without which GitHub issues no OIDC token, and `contents: read` for the checkout.

**Renaming a publishing workflow breaks its publish.** The policy matches the workflow file by name, so a rename or move makes the next push fail at the login step until the policy is edited to match. The failure is before anything is pushed.

**Re-running a failed publish** uses the workflow as it was at the tag, so a run from a tag that predates trusted publishing still reads the retired `NUGET_API_KEY` secret and cannot be finished this way.

## Source-build version

`src/Directory.Build.props` pins all four version properties — `Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion` — to `0.0.0`, so `netpace --version` from a local `dotnet build` or `dotnet run` reports `0.0.0`.

**Why**: `0.0.0` is below every release tag and matches none of them, so a build from source cannot be mistaken for a release.

**Released executables report their tag.** `release-binaries.yml` passes all four properties from the tag to each `dotnet publish`, overriding the placeholder. `netpace --version` reports the informational version, without the `+<commit>` suffix the SDK appends, so a prerelease build reports its suffix too.

**The `NetPace.Core` package reports its tag too.** `publish-nuget.yml` passes the same four properties to its Build step, and the `Verify packed library version` step fails the job if the packed library does not report the tag (see *NuGet package version*).

## Release tags

All three release workflows (`release-binaries.yml`, `publish-tool.yml`, `publish-nuget.yml`) trigger on semver-shaped tags only: `X.Y.Z` and `X.Y.Z-suffix`. A tag of any other shape starts nothing. There is no test-tag form, because every tag is a release: the SDK and NuGet reject a non-semver version before anything compiles.

**Proving a release-pipeline change.** Merge it, then tag a release candidate (`X.Y.Z-rcN`). It runs every workflow and gate and publishes as a prerelease, which `dotnet tool install` and `dotnet tool update` skip by default. If a gate fails, fix it and tag the next rc. Some changes, such as a new macOS or Windows Native AOT entry, cannot be built locally at all, so the rc is their only proof.

## Prerelease tags

A tag of the form `X.Y.Z-suffix` (`0.27.0-rc1`, `0.27.0-rc.1`) is a prerelease. It runs all three release workflows like any other tag: the archives are built and attached to the GitHub Release, and the tool package and `NetPace.Core` are published — every one of them at the prerelease version.

**How the version is split.** `AssemblyVersion` and `FileVersion` accept numbers alone, so each workflow's `Extract version from tag` step also outputs the tag's numeric part (`0.27.0` for `0.27.0-rc1`), and only those two properties take it. `Version`, `InformationalVersion` and `PackageVersion` take the whole tag. For a plain `X.Y.Z` tag the two are identical. Passing the whole prerelease tag to `AssemblyVersion` or `FileVersion` fails the compile (CS7034), which is how a prerelease tag failed before the split existed.

**What users see.** `netpace --version` reports the whole tag, suffix included, from both an archive download and the installed tool. NuGet treats the packages as prereleases: `dotnet tool install -g netpace` and `dotnet tool update -g netpace` skip them, and installing one takes `--prerelease` or an explicit `--version 0.27.0-rc1`.

**A prerelease publishes for real.** Its packages reach nuget.org and stay there (they can be unlisted, not deleted). That is what makes it the proof for a release-pipeline change (*Release tags*).

## SDK version pinning

The .NET SDK version is encoded in two places, both targeting **.NET 10 (LTS)**:

- **`global.json`** (repo root) pins `"version": "10.0.0"` with `"rollForward": "latestFeature"`, so local and CI builds resolve to the latest installed 10.0.x feature band — reproducible without hard-pinning a patch that may not be on every runner.
- **Each workflow** (`dotnet.yml`, `codeql.yml`, `publish-nuget.yml`, `publish-tool.yml`, `release-binaries.yml`, `shell-tests.yml`) sets `dotnet-version: 10.0.x` on `actions/setup-dotnet`, which installs a 10.0 SDK that `global.json` then honours.

When bumping the SDK major, update both places in lockstep.

## Release notes

Per-release "what changed" notes are **GitHub-auto-generated from the PRs merged since the last tag** — there is no `CHANGELOG.md` to maintain. The `<PackageReleaseNotes>` property in both `NetPace.Core.csproj` and `NetPace.Console.csproj` already points NuGet consumers to <https://github.com/FrankRay78/NetPace/releases>, so the auto-generated notes are the single source of truth for both CLI and library audiences. Edit the GitHub Release body manually only when a particular change deserves prose framing the auto-generated PR list can't supply.
