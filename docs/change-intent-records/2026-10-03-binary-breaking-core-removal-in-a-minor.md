# A binary-breaking NetPace.Core removal ships in a 0.x minor

**Intent:** Remove `DelayProviderStub.Reset()`, an unused public method on a type that ships to NuGet, without parking the deletion behind a major version NetPace has no plan to cut.

**Behaviour:** Given a consumer that compiled against `NetPace.Core` 0.25.0 and calls `DelayProviderStub.Reset()`, when it runs against 0.26.0 without recompiling, then it fails with a `MissingMethodException` — thrown when that call site executes, not at assembly load, so the consumer's build and startup both stay green and the break surfaces only on the path that used it. Given any code in this repository, the method had no call sites, so nothing here changes behaviour and the suite is unaffected.

**Constraints:**

- `DelayProviderStub` lives in `NetPace.Core/Clients/Testing/`, which is public, XML-documented and shipped in the package deliberately: `README.md` ("Testing Your Code") advertises the namespace as consumer-facing, though its list names only the four `ISpeedTestService` stubs and not `DelayProviderStub` itself. The type is public either way, so the method was part of the published surface rather than internal scaffolding.
- Constitution VII reserves MAJOR for breaking changes to the public API. The latest tag is 0.25.0 and every GitHub release is marked pre-release, so SemVer's `0.y.z` rule applies and a 0.x minor may carry the break. `src/Directory.Build.props` reads `<Version>1.0.0</Version>`, but that is a build-time placeholder: `publish-nuget.yml` and `release-binaries.yml` both derive the version from the pushed tag and override it at pack time.
- Nothing detects this class of break at build time. `NetPace.Core.csproj` sets `EnablePackageValidation` but no `PackageValidationBaselineVersion`, so package validation has no baseline to compare against and the removal passes silently. Closing that gap is deliberately out of scope here and is recorded on issue #243 as a separate issue to raise.

**Decisions:**

- *Ship it in the next minor (0.26.0) rather than waiting for a MAJOR.* This is the maintainer's decision, recorded on issue #243. Waiting would park a dead method indefinitely, because a pre-1.0 project with no 1.0 milestone has no scheduled MAJOR to attach it to.
- *Treat this as distinct from [`2026-10-02-result-properties-are-required.md`](2026-10-02-result-properties-are-required.md), not covered by it.* That record ships a breaking Core change in 0.x on the same SemVer reasoning, so the precedent is established — but it explicitly scopes itself to a source-breaking change, noting "no signatures change, so already-compiled consumer assemblies keep running." Deleting a public method is binary-breaking: a consumer assembly that is never recompiled still fails, and no compiler warning stands between them and the `MissingMethodException`. The two records agree on the version policy and differ on how far the break reaches, which is the part worth having written down.
- *Delete rather than `[Obsolete]`-then-delete.* An obsoletion cycle is the standard way to soften a binary break, and it was rejected only because the pre-1.0 package carries no compatibility promise to soften. Were NetPace past 1.0, the deprecation step would be the right call for this same method.

**Date:** 2026-10-03
