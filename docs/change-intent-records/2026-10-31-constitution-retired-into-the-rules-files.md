# Constitution retired into the rules files

**Intent:** Give every rule one home and drop the framing that read each as absolute and overriding, so changing a rule is an ordinary edit and agents stop re-arguing settled questions from first principles. Refs #479.

**Behaviour:** `docs/constitution.md` is deleted. Its rules live, unchanged in what they require and in plain language, in [`CONVENTIONS.md`](../../CONVENTIONS.md), [`REVIEW.md`](../../REVIEW.md) and [`CLAUDE.md`](../../CLAUDE.md); none of the three overrides another. Every reference outside the change-intent records is repointed or removed. Earlier records still cite the constitution and its section numbers as history.

**Where each section went:**

| Constitution section | Now |
|---|---|
| I Test-Driven Development, with the configuration/tooling carve-out | `CONVENTIONS.md` → *Test-driven development* |
| II Library-First Architecture | `CLAUDE.md` → *Product Principles*, Library-first |
| III CLI Excellence, incl. one switch per output format and the pre-1.0 clig.dev exception | `CLAUDE.md` → *Product Principles*, CLI excellence and Output formats; "a review must not raise the absence of a removal message" → `REVIEW.md` → *What not to flag* |
| IV Cross-Platform Compatibility | `CONVENTIONS.md` → *Code quality*, Cross-platform |
| V Code Quality Standards | `CONVENTIONS.md` → *Code quality*, Standards |
| VI Minimal Dependencies, incl. the licence rule | `CONVENTIONS.md` → *Dependencies* |
| VII Semantic Versioning and the pre-1.0 stance | `CONVENTIONS.md` → *Public API and breaking changes* |
| VIII AC-to-Test Traceability | `CONVENTIONS.md` → *Acceptance criteria*, Scenario labels and Traceability |
| IX Behavioural Specification, incl. interface contracts and the regression exception | `CONVENTIONS.md` → *Acceptance criteria*, Outcomes not mechanism, Interface contracts, Tests verify the criterion |
| X No Skipped Tests | `CONVENTIONS.md` → *Testing*, No skipped tests |
| Git Workflow | `CONVENTIONS.md` → *Commit messages* |
| Testing Standards, incl. the snapshot rule and the do-not-test list | `CONVENTIONS.md` → *Testing* |
| Authority clause, the NON-NEGOTIABLE / MUST NEVER framing, §VII's "overrides any rule elsewhere", §IX's lockstep clause, every Rationale paragraph | Framing and history, dropped; the records hold the history |

**Decisions:**

- Versioning and the pre-1.0 stance went to `CONVENTIONS.md`, not `CLAUDE.md`, because `/build` reads the stance there and a rule is stated once; `CLAUDE.md` → *Product Principles* points at it. Rejected: a copy in `CLAUDE.md` — two statements of the stance drift.
- Citations in hooks, scripts, workflows and test comments name the file and heading (`CONVENTIONS.md`, Acceptance criteria), never a number, so the citation reads without the retired numbering and a grep for the heading finds every one.
- `scripts/link-check.tests.sh` keeps `docs/constitution.md` as a fixture path: it is test data for the checker, not a reference to the file.

**Date:** 2026-10-31
