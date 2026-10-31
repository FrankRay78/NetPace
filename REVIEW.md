# Review criteria

NetPace's additions to the review method in [`/review-pr`](.claude/commands/review-pr.md) and [`/review-followup`](.claude/commands/review-followup.md). The commands hold the method — severities, tiers, dimensions, verification, the verdict — and read this file for what is particular to this repository. Each section sits under the heading of the part of the method it extends, as the command's *Repository criteria* table lists them, and where a section and the method conflict, this file wins. In CI the review reads `main`'s copy of this file, never the pull request's, so a change here takes effect on merge, and is itself a change that always needs the maintainer.

## Where it runs

`.github/workflows/review-pr.yml` runs `/review-pr` on every pull request `FrankRay78` opens, posting as the workflow's bot. Adding the `review-followup` label runs `/review-followup` in `.github/workflows/review-followup.yml`. Both put `main`'s copy of this file in place before the agent starts.

## Severity

- **Major** includes a breach of a rule `CONVENTIONS.md` states or of a product principle `CLAUDE.md` → *Product Principles* states.

## What not to flag

- Anything a CI check already gates here — build and tests, traceability (`scripts/traceability-check.sh`), issue link — unless the check itself is what the PR breaks. Build and traceability are required checks in the `Main CI/CD` ruleset; `issue-link` runs on every pull request but does not block the merge.
- Breaking changes as such. While NetPace is pre-1.0 they need no approval or deprecation period (`CONVENTIONS.md` → *Public API and breaking changes*). Flag only a PR that does not state what breaks and for whom.
- The absence of a "this option was removed" message for a removed or renamed option. While NetPace is pre-1.0 that message is a preference (`CLAUDE.md` → *Product Principles*), never a gap.

## Risk tier

- A changed snapshot (`src/NetPace.Console.Tests/Expectations/*.verified.txt`) is never Low: a snapshot is a user-visible contract, and a changed one means the output changed. It is Medium at least.
- **High** includes logic in server selection or measurement — server screening, the latency probe and the download and upload measurement under `src/NetPace.Core/Clients/`, which [`docs/architecture/server-screening-and-measurement.md`](docs/architecture/server-screening-and-measurement.md) describes.

## Always a person

- anything under `.github/` or `scripts/`
- the release and publish pipeline, including `docs/RELEASING.md`
- `src/Directory.Packages.props`, and package-reference changes in project files

## Contracts

What a user, a script or a `NetPace.Core` consumer touches directly: a CLI option, an exit code, a CSV or JSON field, a public `NetPace.Core` member. A changed CLI option must update `README.md`, including its `--help` snapshot, and `USER_GUIDE.md`. Also flag reflection-heavy or non-trim-safe code, since NetPace targets AOT-trimmed builds: a finding, not a contract change.

## Context to load

`CLAUDE.md` and `CONVENTIONS.md` always, and the convention docs `CLAUDE.md` points to for the layers the PR touches: `docs/conventions/csharp-style.md` for `.cs` files, `docs/conventions/testing.md` for tests.

## Change map

The areas to group changed files by: the `NetPace.Core` library, the `NetPace.Console` CLI, tests and snapshots, CI workflows and scripts, the Claude harness under `.claude/`, and docs.

## Tests

A test that asserts mechanism rather than outcome breaches `CONVENTIONS.md` → *Acceptance criteria*. Console output belongs in a Verify snapshot, never in hand-rolled string matching, and no test may be skipped (`CONVENTIONS.md` → *Testing*).
