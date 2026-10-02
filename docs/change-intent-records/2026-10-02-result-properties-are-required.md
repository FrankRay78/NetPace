# Result properties are required, and "did not run" is absence

**Intent:** Make it impossible to construct a `SpeedTestResult` or `LatencyTestResult` that reports a value nobody supplied. Both types are published to NuGet, so their construction contract is part of NetPace.Core's public surface.

**Behaviour:** Given a construction site that omits any property of either result type, when the project is compiled, then the build fails with CS9035 rather than producing a result whose missing value silently defaults to zero. Observable output across normal, CSV, JSON and minimal modes is unchanged.

**Constraints:**

- The four console writers each opened with `new SpeedTestResult()` as a placeholder for a test that did not run — eight sites. `required` makes that construction illegal, so each local became `SpeedTestResult?`.
- `--no-latency` still produces a zeroed `LatencyTestResult` from `ServerSelector`, with `LatencyMilliseconds = 0` stated explicitly. Making latency itself optional was deliberately left out of scope: the result carries the selected server, which the writers need whether or not latency was measured, so absence is not available as a representation here without a wider change.
- The change is source-breaking for anyone implementing `ISpeedTestService` or constructing these records, which under Principle VII is a MAJOR concern. No version bump accompanies this change — the release version is a separate decision, and `--version` appears in committed console snapshots.

**Decisions:**

- *Mark all properties, not just `RequestsSucceeded`/`RequestsFailed`.* The counts are the newest additions and the ones PR #222 found unset, but a result missing `BytesProcessed` is no more meaningful than one missing its counts. Marking only the recent additions would read as an accident of history rather than a decision.
- *Writers branch on the nullable result, not on `settings.NoDownload`/`settings.NoUpload`.* Re-checking the flag at each use site and reaching for `!` would keep the compiler quiet while leaving two sources of truth for the same fact. Branching on the result itself means the type carries the answer, and a future flag change cannot desynchronise from it. In `CSVConsoleWriter` this moved the speed and header formatting behind the null check rather than swapping it like for like, because those strings are derived from a measurement that may not exist.
- *The placeholder was the bug, not an obstacle.* A zeroed result is indistinguishable from a genuine all-failed measurement — exactly the confusion that let `VariableSpeedTester` report 0.25 Mbps alongside zero requests attempted, poisoning 11 committed snapshots before a human noticed the impossible CSV row. Representing "did not run" as `null` removes the ambiguity at the type level instead of relying on a convention.
- *Tests assert the emitted `RequiredMemberAttribute` rather than attempting to compile an illegal construction.* `required` has no runtime behaviour, so the only in-suite instrument is the metadata the compiler emits for cross-assembly enforcement — the same metadata a consumer's compiler reads. This follows the existing precedent of `ProfileXmlDocTests`, which asserts against the compiler-emitted XML doc file.

**Date:** 2026-10-02
