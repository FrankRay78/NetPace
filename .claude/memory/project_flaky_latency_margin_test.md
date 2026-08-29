---
name: OoklaSpeedtest latency-margin test is flaky under load
description: OoklaSpeedtestTests latency-margin test flakes under machine load — separate from the ProfileXmlDocTests file-sharing flake
type: project
---

`OoklaSpeedtestTests.GetServerLatencyAsync_ShouldReturnLatency_WhenResponseIsValid_MultipleTestIterations` is flaky. It asserts measured latency lands within a ±25% margin of a simulated `pingDelay` (`OoklaSpeedtestTests.cs:225`), which a loaded machine overshoots — observed at 142ms and 85ms against a 45–75ms band, twice in an 8-run solution-level soak on 2026-08-27.

**Why:** it is a wall-clock assertion, so it measures the host's scheduling latency as much as the code's. It is unrelated to the `ProfileXmlDocTests` `FileShare` flake fixed on `fix/profile-xmldoc-file-sharing` — that one was an `IOException` from a concurrent writer, and it did not recur across ~30 solution runs after the fix. Do not conflate the two: an earlier uncaptured solution-run failure during that work was most likely this test, not the XML-doc one.

**How to apply:** when a solution-level `dotnet test ./src` goes red on one Core.Tests case, check the test name before assuming a regression — capture a TRX (`--logger "trx;LogFileName=x.trx" --results-directory ...`) rather than filtering the console, since these failures are intermittent and the console detail is easily lost. Fixing it properly means removing the wall-clock dependency (inject a clock / assert on the mocked delay), not widening the margin. See [[feedback_rerun_tests_before_done]].
