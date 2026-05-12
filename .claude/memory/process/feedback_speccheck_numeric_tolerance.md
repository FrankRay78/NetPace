---
name: Challenge speculative numeric tolerances in test plans
description: When spec or test-plan documents prescribe a numeric tolerance (e.g. "within 1e-9"), challenge whether the operation under test actually introduces FP error before mirroring it into the test code.
type: feedback
---

When `/speckit.testplan` or any spec author prescribes a numeric tolerance (`within 1e-9`, `±0.001`), challenge it before mirroring it into test code. For doubles parsed from string literals via `double.Parse(..., InvariantCulture)` the round-trip is bit-exact; a tolerance hides drift bugs without catching the bug under test. Default to exact equality and only add tolerance when the operation actually introduces FP error (accumulation, division, transcendentals).

**Why:** During feature 001-linux-aot-release, FR-010's "Parser uses invariant culture for numeric attribute parsing" scenario in `test-plan.md` prescribed `(within 1e-9)` tolerance for asserted lat/lon values. The implementation used `double.Parse(..., NumberStyles.Float, InvariantCulture)` whose round-trip is bit-exact against the C# literal compiler path, making tolerance both unnecessary AND counterproductive — culture leakage produces wildly wrong values (51, 515074, or an exception), not slightly-off ones, so tolerance can't soften those into a pass. Reviewer caught it after the fact; should have been spotted at code-review time.

**How to apply:** Before copying any tolerance from spec or test-plan into a `ShouldBe(value, tolerance)` / `Assert.Equal(expected, actual, precision)` call, ask: does the operation under test actually introduce floating-point error? Round-tripping a parsed numeric literal does not. Accumulation across many additions does. Division does. Transcendentals do. Default to exact equality (no tolerance overload) unless you can name the FP-introducing step. If a tolerance turns out unnecessary, remove it from both the test code and the matching `#### Scenario:` in `test-plan.md` in the same commit.
