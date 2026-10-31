---
description: Whole-codebase audit across five lenses (conformance to the repository's stated rules, test-suite integrity, docs against code, harness consistency, dead code and duplication). Every finding is challenged by a separate agent before it is reported. Run after a batch of merges or before a release, not per-PR. Records findings only — changes no file, and raises one GitHub issue.
---

Read `CLAUDE.md` for project context, and `CONVENTIONS.md` as *Repository conventions* below says, before proceeding.

`/audit-codebase` is the **whole-codebase** counterpart to the diff-shaped review tools. `/verify`, `/review-slop`, `/code-review`, `/security-review` and the `pr-review-toolkit` agents all take "changes since main" as input, so on a merged `main` they have nothing to read. This command takes the committed tree itself as input, and is the tool for the moment a run of merges has landed and the question is what they add up to.

**This command records findings only. It changes no tracked file, deletes nothing outside its own working directory, and opens no pull request.** Every fix is separate, individually scoped work. That keeps the audit cheap to re-run and safe to leave running.

**Findings live in one GitHub issue, not in the repo.** A committed checklist goes stale the moment its first item is done, and the work is tracked in the issue tracker anyway. Working files go under `.claude/scratch/audit/`, which is git-ignored.

## Argument

`/audit-codebase <since-ref>` — a tag or commit that starts the "what changed" window, e.g. `/audit-codebase 0.25.0`. The lenses still read the whole tree; the window only tells them what is new. If no argument is given, or `git rev-parse --verify <since-ref>^{commit}` fails, ask for one and stop. Do not guess, and do not default to the latest tag.

## Repository conventions

The method in this file is generic. What is particular to the repository it audits — which paths are in scope, which tools supply evidence, which rules the code is held to, which documents describe which code, what a lens should know about this tree before it starts — lives in `CONVENTIONS.md` at the root of that repository, written as additions to the steps. Read it before step 0; each step then applies its own rule together with the section of `CONVENTIONS.md` under the matching heading, and where the two conflict, `CONVENTIONS.md` wins. The headings this command reads:

| Heading | Supplies |
|---|---|
| `Audit` | the scope, the evidence commands, the rules lens 1 audits, what each lens should know about this repository, and the label the issue carries |
| `Testing` | the testing guide lens 2 reads first |
| `Dependencies` | the dependency-controlled library whose dependencies are in scope |
| `Public API and breaking changes` | the published library whose public surface lens 5 walks |

A section under any other heading is read too, and applied where its text says. Where the file or a heading is missing, the step concerned runs on its generic rule alone, and the report to the user names each heading not found. A step whose convention is the thing itself — the file to write, the label to apply — has no generic rule, so where that heading is missing the command stops and names the heading rather than invent one. `CONVENTIONS.md` is read from the root of the repository this command runs in, and trusted as this command file is.

## Scope

**In scope:** the tracked paths `CONVENTIONS.md` → *Audit* lists, and where it lists none, every tracked file.

**Out of scope — do not report findings on these:**

- **Security.** Vulnerability hunting is left to a dedicated security scanner and to whatever advisory and code-scanning gates CI already runs (`CONVENTIONS.md` → *Audit* names them). Do not raise injection, secrets, or advisory findings, and do not treat their absence from the report as a gap.
- **Mutation testing.** Not an evidence source for this command.
- Build output, working files and worktree copies — `.claude/scratch/`, `.claude/worktrees/` and the build directories. A worktree is a full second copy of the tree; auditing it would double every finding.
- `docs/change-intent-records/` as a target for staleness findings. A record describes a decision as it stood on its date and is not expected to track the code afterwards. Lenses may *read* the records to learn why something is the way it is.

**Dependencies are in scope** where `CONVENTIONS.md` → *Dependencies* names a dependency-controlled library: a deprecated or outdated package, an unjustified dependency of that library, or a licence its rule does not allow is a finding.

## Steps

0. **Preconditions.** Run `git status --porcelain`. If it prints anything, report the dirty tree and stop — an audit of a half-edited tree produces findings that describe no committed state. Record `git rev-parse --short HEAD`, the current branch, and today's date; all three go in the issue header so a later run can be compared with this one. Run `mkdir -p .claude/scratch/audit`.

1. **Establish the "what changed" window.** From `git log --oneline <since-ref>..HEAD` and `git diff --stat <since-ref>..HEAD`, summarise what is new or materially changed since `<since-ref>`: product code by project, tests, docs, slash commands, hooks, scripts, workflows, and any amendment to a document that states the repository's rules. Write the summary to `.claude/scratch/audit/window.md` and give it to every lens — a lens that cannot tell new code from old will report standing debt as though it had just arrived, and a later audit will re-report this one's findings.

2. **Gather evidence from the tools that already decide a question.** Run each command in the evidence table `CONVENTIONS.md` → *Audit* gives, from the repository root, and save its output under `.claude/scratch/audit/`. They are evidence for the lenses to interpret, not findings in themselves. If a tool fails to start, exits abnormally, or its command is refused by the permission settings, do not retry it more than once and do not stop: record the tool, the error, and which lens loses evidence, and carry that into the issue's *Method and its limits* section.

   A red build or a failing test is not a reason to stop, but it is the first item in the issue: say plainly which, with the output. Where the table says how a report is to be read — which packages a coverage report covers, for instance — pass that on to the lens it feeds.

   Run `git status --porcelain` again once the tools have finished. It should print nothing. If a tracked file changed, or an untracked file appeared outside the ignored paths, a tool wrote where it should not have. Do not revert or delete it: stop, and report the file and the tool to the invoker, who decides what to do with it.

3. **Run the five lenses as parallel clean-context subagents.** Launch all five in a single message so they run concurrently. Use `subagent_type: "general-purpose"` — a lens must reason about conformance, not merely locate code, which is outside `Explore`'s remit. Give each lens: the window summary from step 1, the scope and exclusions above, the paths of its evidence files from step 2, its brief below, and what `CONVENTIONS.md` → *Audit* says for that lens. Tell each lens:

   - It is read-only. It edits nothing and runs no command that writes to a tracked file.
   - Return findings as structured rows: file, line, what is wrong, why it matters, severity (`broken` — a user of the product or the library gets wrong behaviour today; `drift` — two things that should agree do not; `debt` — it works, and it will cost later), which rule or precedent it breaks, and whether it falls inside the window.
   - Every finding cites a concrete file and line. An uncited finding is not admissible.
   - Report what it checked and found sound, not only what failed. A lens that reports only failures gives a false picture of a partial success.
   - Report a rule that looks absent as a question, not a finding, unless a document says the rule should be there. An absence is often the design. A question is still a row with a file, a line and a factual claim, and it goes through step 4 like any finding.

   **Lens 1 — Conformance to the stated rules.** Check the code that claims to follow each rule the repository states as mandatory against what the rule says, concentrating on any amended inside the window. `CONVENTIONS.md` → *Audit* names the document that states the rules and, for each, what to look for; where it names none, read the documents `CLAUDE.md` points to as the project's rules and derive the checks from them.

   **Lens 2 — Test-suite integrity.** Read the testing guide `CONVENTIONS.md` → *Testing* names first; it lists the failure modes that pass while verifying nothing. Then:
   - **Outcome, not mechanism** — would this test survive a different reasonable implementation of the same behaviour? Report tests that pin call counts, private structure, or exact internal sequencing without a named regression to justify it.
   - **Snapshots** — a committed snapshot that is empty, that no test reads, or whose test would pass whatever the output said; output asserted by hand-rolled string matching where the repository's rules require a snapshot.
   - **Traceability, reverse direction** — a `SCENARIO:` marker that names no label in any issue. `scripts/traceability-check.sh` checks label → marker for one branch and deliberately leaves marker → label to review; this lens is that review, across the whole suite. A marker carries no issue number, so find its issue from the commit that introduced it: `git log -S'SCENARIO: <name>' --format='%h %s'` gives the commit, whose subject carries `Refs #N` or the pull request number, and `gh issue view` or `gh pr view` gives the issue body. Report only a marker whose issue was identified this way and does not carry the label; a marker whose issue cannot be identified is counted in what the lens could not reach, not reported. Ignore the markers `CONVENTIONS.md` → *Audit* names as fixtures and examples of the convention.
   - **Determinism** — a test that depends on wall-clock time, real network, ordering between tests, or shared files. Where `CONVENTIONS.md` → *Audit* names a seam for measuring time, a wall-clock delay standing in for a duration is a finding rather than a necessity.
   - **Coverage, as a pointer only** — from the coverage evidence, name the public members and whole output paths with no covering test, in the packages `CONVENTIONS.md` → *Audit* says to read. Do not report a percentage as a finding, and count a snapshot test as coverage for the output mode it exercises.
   - **Shell test matrices** — does every `*.tests.sh` under `scripts/` and `.claude/hooks/` exercise the script it is named for, and does each script that has a matrix still match the cases the matrix asserts?

   **Lens 3 — Docs against code.** For each statement a document makes about behaviour, check the code or workflow does that. Report the divergence and both sides of it; **do not assume which side is right** — the lens cannot know whether the doc is stale or the code regressed, and saying so is more useful than guessing.
   - Each document-to-code pairing `CONVENTIONS.md` → *Audit* lists: user guides against the real product, package readmes and samples against the public API, architecture documents against the code they describe, release documents against the release workflows.
   - `CLAUDE.md`, `CONVENTIONS.md` and `REVIEW.md` against the tree: every path, type, signature and command they name.
   - Anywhere a document refers to an option, type, command or file that no longer exists.

   **Lens 4 — Harness consistency.** The slash commands in `.claude/commands/`, the skills in `.claude/skills/`, the hooks in `.claude/hooks/`, `.claude/settings.json`, the scripts in `scripts/`, the workflows in `.github/workflows/`, the project memory in `.claude/memory/`, `CONVENTIONS.md`, `REVIEW.md` and the workflow documents `CONVENTIONS.md` → *Audit* names are meant to read as one system. They have changed faster than anything else in the repository, one branch at a time. Read them as a set and report:
   - **Contradictions** — two commands, or a command and a document, that give different rules for the same thing: branch naming, commit message form, labels, when to stop, how many review rounds, what counts as a blocking finding.
   - **Dangling references** — a command, label, script, hook, file, section heading or step number that something points at and that no longer exists or has been renamed. Check labels against `gh label list`, and the headings a command's *Repository conventions* or *Repository criteria* table names against `CONVENTIONS.md` and `REVIEW.md`.
   - **Described versus actual** — a hook or script that a document describes as doing one thing while its source does another; a permission or hook registration in `.claude/settings.json` that a document describes differently.
   - **Hand-offs** — where one command's output is another's input (`/draftissue` → `/reviewissue` → `/confirmissue` → `/build` → `/verify` → `/raise-pr`, and the chain scripts that drive them): does each still produce what the next one reads, in the form it reads it?
   - **Stale memory** — an entry under `.claude/memory/` that the code, a command or a later entry has made false, and an index line in `MEMORY.md` with no file or a file with no index line.
   - **Workflows** — a job that references a script, path or label that has moved, and a required check the documents name that no workflow produces.

   Leave bloat, verbosity and "could be shorter" to `/context-gardening`. This lens reports where the harness disagrees with itself or points at nothing.

   **Lens 5 — Dead code and duplication.** Start from the tool evidence, then confirm each candidate by reading.
   - **Unused symbols** — treat the unused-symbol list as candidates only. It can report members that a source generator or a framework consumes as unused; `CONVENTIONS.md` → *Audit* names the known cases. Discard those, then for each remaining candidate search for every reference, including string-based and reflective use, before reporting it.
   - **Public surface** — the tool does not look at public members. Walk the public API of the published library `CONVENTIONS.md` → *Public API and breaking changes* names and report members nothing in the repository uses, **as a question for the maintainer, not as dead code**: a published library's public member may have external callers.
   - **Files and assets** — tracked files nothing references: source files outside every project, snapshots whose test is gone, scripts no workflow, command or document invokes, resources nothing embeds or packs.
   - **Duplication** — from the clone report, group clones and read each group. Report **semantic drift between near-copies** — two copies that were once the same and now differ in behaviour — ahead of plain duplication; the drift is the danger. Repetition inside test arrangement code is a finding only where the copies have drifted or a helper already exists and is not used.
   - **Baseline** — look for a previous audit issue (`gh issue list --state all --search "Codebase audit in:title"`). If there is one, report only what is new since it, plus anything it listed that has since become live. If there is none, this run is the baseline: report everything confirmed, and say so in the issue.

4. **Verify every finding before it is reported.** Reviewer severities are not reliable enough to relay, and a lens is briefed to find things. So no lens output goes into the issue unchallenged.

   For each finding, spawn a fresh clean-context subagent whose brief is to **refute** it: read the cited file and line, and decide whether the finding describes real committed behaviour or a misreading. Default to refuted when uncertain. Launch the verifiers in parallel, one per finding, and pass each the finding *only* — the file, the line, and the one-sentence claim — never the lens's reasoning, so it forms an independent view. Ask each verifier for a verdict (`confirmed` or `refuted`), a one-line reason, and a corrected severity if the lens's was wrong.

   A finding survives only if its verifier confirms it. Drop the rest, keeping each with its reason for the issue. Questions for the maintainer are verified the same way: the verifier checks the factual claim under the question, such as "nothing in the repository references this member". For each lens count three numbers: raised (rows the lens returned), verified (rows a verifier returned a verdict on — fewer than raised only if a verifier failed, which is itself reported), and survived (rows confirmed).

   This step costs one agent per finding and is not to be thinned, sampled or batched several-findings-to-an-agent to save cost. A verifier holding several findings stops being independent of any of them.

5. **Raise one GitHub issue.** Compose the body at `.claude/scratch/audit/issue-body.md`, then create it with `gh issue create --title "Codebase audit: <short HEAD> (<date>)" --label <label> --body-file .claude/scratch/audit/issue-body.md`, with the label `CONVENTIONS.md` → *Audit* names. One issue per run, whatever the run found.

   The body, in this order:

   - **Header** — the commit, branch and date audited, the `<since-ref>` window, and one paragraph on what the run found.
   - **Broken for users today** — only findings whose verified severity is `broken`. If there are none, keep the heading and say so. A red build or failing suite from step 2 goes here first.
   - **Everything else** — **numbered checkbox lists** (`1. [ ]`), in sections by **decreasing priority**, numbered continuously across sections so an item can be referred to by number. Each checkbox stands alone: what, where (`file:line`), and why it matters, without needing the others for context. Rank by leverage, not by severity count — an item that removes a standing class of problem ranks above a one-line fix, and an item that closes several findings at once ranks above both. Separate the few items that need a design decision from the mechanical sweeps, and say which is which.
   - **Questions for the maintainer** — unused public members of the published library, and absences a lens raised as a question. Not checkboxes; they are decisions, not work.
   - **What came back clean** — a lens finding nothing is a result, and a gate-enforced rule holding is worth stating.
   - **Refuted — do not re-raise** — each dropped finding with the verifier's reason, so a later audit does not file it again.
   - **Method and its limits** — per-lens raised, verified and survived counts as a table; every tool from step 2 that did not run and what that lens therefore could not check; anything a lens did not reach; and the standing exclusions with their reasons: **security is out of scope because a dedicated security scanner and CI's advisory and code-scanning gates cover it, and mutation testing is not run.** Writing the exclusions down is what stops a later audit from helpfully raising them as gaps.

   **Labels.** Apply the one label `CONVENTIONS.md` → *Audit* names, and nothing else. Never `ready`, `review`, `confirm` or `parked`: `ready` hands the issue to the unattended chain runner, which builds and raises a pull request with no human deciding, and `review` and `confirm` trigger the automated issue-review workflows, which expect one feature's acceptance criteria and not a backlog.

   Author each paragraph, bullet and table row as a single unwrapped line. Link only to files that exist at the audited commit, by path relative to the repository root. Do **not** commit the body or any working file.

6. **Report to the invoker.** The issue URL; findings raised against survived, per lens; whether anything is broken for users today; the top three items by leverage, one line each; and anything a lens or tool could not reach, with the reason. If a lens returned nothing, say so plainly — a clean lens is a result, and a lens that found nothing across a whole tree is itself worth a second look. Call out any lens with a poor survival rate and what its refuted findings had in common: that is a defect in the lens's brief, and fixing the brief is the cheapest improvement available to the next run.

   Finish with `git status --porcelain` and state its result. If it is not empty, list what it printed; do not clean it up.

## When to run this

After a batch of merges has landed on `main`, or before tagging a release — the point at which the diff-shaped tools have nothing to read and the accumulated shape of the tree is the thing worth examining. Not per-PR: `/verify` already reviews every branch, and running this per-PR would re-report the same standing debt every time.
