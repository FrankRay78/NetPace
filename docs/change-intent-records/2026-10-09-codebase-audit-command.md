# A whole-codebase audit, with every finding refuted before it is reported

**Intent:** Give NetPace a review that reads the committed tree rather than a branch diff. Every existing reviewer takes "changes since main" as input, so once a branch merges nothing looks at what a run of merges adds up to. Issue #358 has the evidence: 46 commits on `main` in the 30 days to 2026-10-09, with about 129 file touches under `docs/`, 110 under `.claude/` and 31 under `scripts/`, against 31 across the two product projects.

**Behaviour:** [`/audit-codebase`](../../.claude/commands/audit-codebase.md) specifies it.

**Constraints:** The command changes no tracked file and opens no pull request. It adds no repository configuration: no tool manifest, no workflow, no committed report.

**Decisions:**

- *Ported from another repository's command, keeping the method and replacing the lenses.* The maintainer's IMS repository has a command of the same name whose one run raised 42 findings, of which a refuting verifier dropped 6 and re-graded 8. Parallel clean-context lenses, one verifier per finding given the claim alone and defaulting to refuted, and a "refuted, do not re-raise" list are carried over unchanged. All five lens briefs were rewritten. Four were about that system's ledger and tiers and have no counterpart here; test-suite integrity is the only one whose subject carried over.
- *A harness lens, which the original has no equivalent of.* The slash commands, hooks, scripts and workflows are where most recent change landed, each branch reviewed alone. It reports contradictions and dangling references only; bloat and verbosity stay with `/context-gardening`.
- *Dependencies in scope, the reverse of the original.* That repository excludes them because it deploys to an isolated instance. `NetPace.Core` is published to NuGet, so Constitution VI makes its dependencies and their licences a finding.
- *Security out of scope.* A dedicated security scanner does that job with its own verification, and CI already fails on a known advisory and runs CodeQL. A sixth lens would duplicate it less well. The exclusion is written into every audit issue so a later run does not raise it as a gap.
- *Mutation testing left out.* It is the one tool that shows a test asserting nothing, but it is slow and has not been shown to work with this suite's runner. Recorded as future work in #358.
- *One issue per run, not one per urgent finding plus a backlog.* The original raises a separate issue for anything that blocks a demo. Here anything broken for users today takes the first section of the single issue instead, which keeps the tracker quiet at the cost of a real break sitting inside a longer document.
- *The window argument is mandatory.* Defaulting to the latest release tag was considered and declined: a wrong window makes every lens misjudge what is new, and the command asking once is cheaper than a run built on a guess.
- *Evidence tools run one-shot at pinned versions.* Roslynator CLI 1.0.1, nuget-license 4.0.18, jscpd 5.4.1 and dotnet-coverage 18.0.6 were each run against this tree on .NET 10 before being written into the command. A tool manifest would make them reproducible but adds configuration for a command run a few times a year.
- *Coverage comes from a wrapper, not a collector.* The test projects reference no coverage collector, so the `--collect:"XPlat Code Coverage"` form runs the suite and collects nothing. Wrapping the run in `dotnet-coverage` produces the report without adding a package to either test project.
- *A tool that writes outside the working directory stops the run.* The command neither reverts nor deletes what it did not knowingly create; the invoker decides.
- *One verifier per finding is not to be thinned.* It is the cost of the run and the reason its output can be trusted; a verifier holding several findings is no longer independent of any of them.
- *Not run as part of this change.* The first real run is the maintainer's, after merge. Its per-lens survival rates are the first evidence of how well each brief works.

**Date:** 2026-10-09
