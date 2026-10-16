# The .NET tool package ships with the binaries

**Intent:** Give anyone with the .NET 10 SDK a one-line install and update for NetPace, without making the tool slower to start or heavier than the release downloads.

**Behaviour:** Specified in [#388](https://github.com/FrankRay78/NetPace/issues/388) — `dotnet tool install -g NetPace` puts a `netpace` command on the path, `dotnet tool update -g NetPace` moves to the latest release, `dnx NetPace` runs it once and leaves nothing installed, and every tag publishes the package at the release's version.

**Constraints:**

- Native AOT cannot be cross-compiled across operating systems or architectures, and the package should carry the native build wherever one exists.
- macOS has no Native AOT build (out of scope), and the package must still install and run there.
- `NetPace.Core` is published by `publish-nuget.yml`, which deliberately skips tags where the library did not change.
- The package ID users type is a contract; `NetPace` and `NetPace.*` are reserved prefixes on nuget.org.

**Decisions:**

- Published the tool from `release-binaries.yml`, not `publish-nuget.yml`. The tool *is* the CLI: it ships on the same trigger as the binaries, from the same native builds, behind the same gates. Adding it to the library workflow would have meant either inheriting the "skip when `NetPace.Core` is unchanged" condition — wrong for a CLI user installing by version, and the common case is a CLI-only tag — or carrying a second, opposite condition in the same file for a reader to untangle.
- Packed each native RID package in the AOT matrix entry that already builds that RID, and left the wrapper and the two macOS packages to a final job. Native AOT forces a runner per RID either way, so the alternative was a second matrix duplicating the first's `runs_on` map.
- Accepted a second AOT compile in each AOT matrix job rather than packing from the archive's publish output. A tool package is packed from the project, not assembled from loose files; teaching the pack to adopt a pre-built binary would mean hand-writing the package layout and `DotnetToolSettings.xml` the SDK already generates — more machinery to maintain than one extra parallel compile costs.
- Gave macOS framework-dependent RID packages rather than a RID-agnostic fallback inside the wrapper. The wrapper is what every platform downloads first; an implementation inside it would put a seventh copy of NetPace in front of every user to serve the platforms the release does not build for anyway. The cost is that an unlisted platform — Alpine-style Linux — cannot install the tool at all, which USER_GUIDE.md states, and those users already have a standalone download.
- Declared the RIDs as `RuntimeIdentifiers` rather than `ToolPackageRuntimeIdentifiers`. Both tell the pack which RID packages to create, but only the former is read by restore, and a per-RID pack needs RID-specific assets; with `ToolPackageRuntimeIdentifiers` alone, the pack fails on a restore that was RID-agnostic.
- Suppressed the AOT native symbol file in the publish directory (`CopyOutputSymbolsToPublishDirectory=false`) instead of filtering it out of the package. It is around twice the size of the binary, and the AOT targets copy it in after `Publish` has finished, where no publish-output filter can reach it. The release archives never carried it — they whitelist by name — so this only ever mattered once something packed the whole directory.
- Verified the package set by installing it and asking the command its version, rather than by inspecting the packed files. One run proves what seven separate file assertions could not: that the wrapper resolves a RID package, that the package carries a runnable binary, and that the binary reports the tag rather than the `0.0.0` source-build placeholder.

**Date:** 2026-10-16
