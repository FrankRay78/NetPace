---
name: Verify-snapshot tests count as coverage in NetPace.Console.Tests
description: When auditing NetPace.Console output-mode coverage (JSON/CSV/text), check Expectations/*.verified.txt before reporting "untested"
type: feedback
---

When a coverage-review agent claims a `NetPace.Console` output mode (JSON, CSV, or rich-terminal text) is untested, verify against `src/NetPace.Console.Tests/Expectations/*.verified.txt` before relaying the finding. The project uses Verify snapshot tests as the primary contract for output-mode behaviour; direct-unit-test searches will miss them.

**Why:** During the PR review for branch `001-linux-aot-release`, a review agent flagged the `JsonConsoleWriter` source-gen switch (`JsonResultIndentedContext` vs `JsonResultCompactContext`) as "Important — untested" because no unit test asserted indented-vs-compact output. The user corrected this: 15+ `.verified.txt` snapshots already cover both `--json` and `--json-pretty` permutations across continuous, multi, scale, hostname, and IPAddress variants. The agent only searched for direct-assertion tests and missed the entire snapshot suite.

**How to apply:** Whenever an automated coverage agent reports a `NetPace.Console` output behaviour as untested, before relaying it to the user, glob `src/NetPace.Console.Tests/Expectations/*.verified.txt` for filenames covering the same surface (e.g. `*.Json.*`, `*.CSV.*`, `--json-pretty`, `--json`). If snapshots exist, downgrade or withdraw the finding rather than promoting it as Important.
