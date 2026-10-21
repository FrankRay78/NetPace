# macOS Native AOT

**Supersedes:** [`2026-10-16-tool-package-ships-every-release.md`](2026-10-16-tool-package-ships-every-release.md) — the decision to give macOS framework-dependent RID packages, and the four-entry native pack matrix that followed from it; and its statement that `publish-nuget.yml` skips tags where the library did not change (decision 8).

**Intent:** Ship NetPace as a Native AOT binary on macOS, for both Intel (`osx-x64`) and Apple silicon (`osx-arm64`), on the same terms as Windows and Linux: a `-aot` release archive per architecture, and a `NetPace.<rid>` tool package carrying the native binary rather than a framework-dependent build. macOS was the only supported platform without one, and the reason recorded when AOT was introduced was "out of scope" with nothing behind it.

**Behaviour:**
- Given: a tag is pushed
- When: `release-binaries.yml` runs to completion
- Then: the GitHub Release carries 18 archives — the 16 that existed before plus `netpace-{ver}-osx-x64-aot.tar.gz` and `netpace-{ver}-osx-arm64-aot.tar.gz`, each holding exactly one native `netpace` binary. Each is smoke-tested (`./netpace --version`, `./netpace --help`) on a Mac runner of its own architecture, and each is asserted smaller than the `standalone` archive for the same platform. A failure anywhere aborts the release.
- Given: a Mac, Intel or Apple silicon, with the .NET SDK installed
- When: the user runs `dotnet tool install -g netpace` or `dnx netpace`
- Then: the installed `netpace` is a Native AOT binary that starts without a .NET runtime, and `netpace --version` reports the release tag.

**Constraints:**
- Native AOT cannot cross-compile between operating systems, so each macOS RID is built on a Mac runner. That makes the native-runner list in `release-binaries.yml` and `publish-tool.yml` six entries rather than four, and the two files still have to change together.
- No change to the macOS `standalone` or `net10` archives.
- The package-shape gate's two-entry count stays as it is: the binary and its settings file, nothing else.

**Decisions:**

1. **A Mac runner per architecture** — `macos-15-intel` for `osx-x64`, `macos-latest` for `osx-arm64`.
   - Rejected: building both on Apple silicon. The compile is possible, but the smoke test would then run the x64 binary under Rosetta, whose availability on hosted runners is unconfirmed — the check most likely to break would be the one meant to catch breakage.
   - Hosted Mac runners are free for public repos, so the second runner costs nothing.

2. **Architecture guard on the macOS jobs.** The existing runner guard compares `RUNNER_OS` against the RID's operating system, which catches a `runs_on` typo falling back to `ubuntu-latest`. The two macOS RIDs share an operating system, so that check cannot tell their runners apart; `uname -m` is compared as well. Nothing else in the pipeline would notice the two Mac runners being swapped.

3. **An install-and-run check on each Mac, inside `pack-native`.** The `publish` job's install check runs on its own runner and so only ever exercises `linux-x64`; before this change the macOS packages were cross-packed framework-dependent, so no macOS binary was ever executed by the tool workflow. Each Mac now packs the wrapper beside the RID package it has just built, installs through it, and requires the reported version to equal the tag — the Linux check's pass condition.
   - Rejected: adding the same check to the Windows entries. They have the same gap, but closing it is a separate change.

4. **Notarisation left out.** The binaries are ad-hoc signed by Apple's linker, which is what Apple silicon requires to run them. An archive downloaded through a browser still raises the Gatekeeper warning, exactly as the macOS `standalone` archives already do, and files laid down by `dotnet tool install` carry no quarantine flag. Buying and plumbing an Apple Developer ID would change the download experience only, and is tracked separately if it is wanted.

5. **A universal (fat) binary is not built.** Two per-architecture archives match how every other platform ships and keep the naming convention uniform.

6. **When the Intel runner retires, Intel Macs revert rather than lose the tool.** `macos-15-intel` is GitHub's last x86_64 macOS image, supported until around August 2027. At that point `osx-x64` drops out of both AOT matrices and `NetPace.osx-x64` goes back to a framework-dependent cross-pack in the `publish` job. Intel users keep a working `dotnet tool install` and keep the `standalone` and `net10` archives; they lose the `-aot` download and the runtime-free start. Recorded in [`docs/RELEASING.md`](../RELEASING.md), *When the Intel Mac runner retires*, so the exit is written down before it is needed.

7. **The gates are proven on a tag, not locally.** Native AOT for macOS cannot be produced on a Linux or Windows machine, so no local run can exercise the new matrix entries. They are proven by the first release candidate after merge (`X.Y.Z-rcN`), which runs every archive and tool-package gate; a failure is fixed and re-proven with the next rc. Rejected: a non-semver test tag — NuGet rejects a non-semver version at restore, so no such tag can build, and every release workflow now triggers on semver-shaped tags only (`docs/RELEASING.md`, *Release tags*).

8. **`NetPace.Core` publishes on every release tag.** Proving the gates with an rc (decision 7) made a latent fault routine: `publish-nuget.yml` published only when the library differed from the nearest earlier tag, and after `0.28.0-rc1` and `rc2` that tag was `0.28.0-rc2`, so `0.28.0` on `main` would have skipped the stable library although it changed since `0.27.0`.
   - Rejected: excluding prerelease tags from the previous-tag lookup. It fixes the rc case but keeps a skip that also misses changes to `src/Directory.Build.props` and `src/Directory.Packages.props`, and before 1.0 a version number promises no compatibility for the skip to protect.

**Date:** 2026-10-18
