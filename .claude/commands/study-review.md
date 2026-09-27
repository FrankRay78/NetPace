---
description: Analyse the docs/study/ records, group recurring problems, score each by recurrence × cost, and propose a numbered list of fixes — then implement or raise as issues the ones you pick.
---

Read `CLAUDE.md` for project context before proceeding.

Run this periodically — after several issues have merged with study records, or before planning harness work. `/study` writes one row per surprise into `docs/study/<N>.md`; this command reads those rows back, following `docs/study/README.md` → *How to read the files back*, and turns them into a ranked list of fixes for you to choose from.

It is the reading half of the study loop, and deliberately different from its neighbours: `/study` records and never acts; `/capture-learnings` starts from one session's corrections; `/study-review` starts from the accumulated rows across many issues.

**It keeps no state.** Every run analyses every row in the folder. It never edits, deletes or annotates a study record — which rows have been dealt with is the invoker's to manage.

**Every proposal must be grounded in rows.** A fix that no row supports is not proposed, however sensible it sounds. Each proposal cites the rows it came from, so the invoker can check the evidence.

---

## User Input

```text
$ARGUMENTS
```

Optional. Empty is the normal case: analyse every record. Issue numbers (`270 280`, or `#270`) restrict the analysis to those records.

---

## Steps

1. **Preconditions.**
   - Confirm `docs/study/README.md` exists and read it — it defines the four levels. If it is missing, STOP and report. Never invent level definitions.
   - List `docs/study/[0-9]*.md` (or the files named in `$ARGUMENTS`). If there are none, STOP and report "No study records to analyse."
   - Run `git status --porcelain` and keep it as the **baseline**, for step 8.

2. **Load the rows.** Parse every table row from each file into `(issue, row number, Finding, Level, Fix applied)`, where row number counts data rows from 1. Refer to a row as `<issue>#<row>` (e.g. `280#4`) from here on. A row whose Level is not one of the four is reported under *Record hygiene* and still loaded.

3. **Tally.** Follow README read-back steps 1, 3 and 4:
   - Count rows per level, overall and per file.
   - Recover the denominator: `gh pr list --state merged --limit 500 --json number,mergedAt` restricted to PRs merged on or after the earliest study file's first commit (`git log --diff-filter=A --format=%aI -- docs/study/<N>.md | tail -1`). If `gh` fails, report the denominator as unavailable with the error — never estimate it. Report rows per merged PR alongside it — the rate, not the raw count, is what one run can be compared against the next with.
   - Name any level with zero rows.

4. **Cluster by repeated shape.** Follow README read-back step 2: group rows that describe the *same class of mistake*, even when the issues, files and wording differ. A cluster is named in one line by the mistake, not by the symptom ("a new rule checked against the first-run path but not the refine-run path", not "pipe escaping"). A cluster may span levels; its level is the one most of its rows carry. A row can belong to only one cluster; a row that shares a shape with nothing is a cluster of one.

   **A cluster must be narrow enough to take one fix.** If its rows would need fixes in different files, or different mechanisms, split it. A broad cluster ("claims not checked", "rules not reconciled") inflates Recurrence and will be cut differently on the next run, which makes runs incomparable.

   Rows that already say a finding was flagged for `/capture-learnings`, or that name "the second occurrence of that shape", are strong clustering signals — use them.

5. **Propose a fix per cluster, and check it at HEAD.**
   - Choose where the fix belongs from the cluster's level, per the README table: Execution → a command prompt, gate or tool; Plan-spec → how issues are drafted (`speckit.draftissue.md`, `speckit.reviewissue.md`); Codebase → the code itself; Environment → the environment, or how the harness depends on it. Prefer a deterministic mechanism (hook, CI check, test, script) over a prompt rule, and a prompt rule over a memory entry — the same order as `/capture-learnings`.
   - Name the specific file the fix would change, and **read it**. If the gap has already been closed at HEAD, drop the cluster and list it under *Already addressed at HEAD* with the file and line that closed it. Do not propose a fix to text you have not read.
   - **Find the existing mitigation.** Search `CLAUDE.md`, the constitution, `.claude/memory/`, `.claude/commands/`, `.claude/hooks/` and `.github/workflows/` for a rule or gate that already targets the cluster's mistake. If one exists, date it (`git log --diff-filter=A --format=%aI -- <file> | tail -1`, or the commit that added the rule) and compare with the date each cluster row's study file gained that row (`git log -S '<distinctive phrase>' --format=%aI -- docs/study/<N>.md | tail -1`). If any row postdates the mitigation, the cluster is **mitigation failed**: name the mitigation, and propose a fix on a stronger tier than it — a prompt rule over a failed memory entry, a deterministic gate over a failed prompt rule. Never propose the same tier again.
   - **Merge proposals that edit the same place.** Two clusters whose fixes change the same section of the same file become one proposal, listing both clusters' rows.
   - Recommend a route: **Implement** when the fix is a contained edit to a prompt, doc or config file; **Issue** when it touches `src/`, spans several commands, or needs a decision the rows cannot settle.

6. **Score each cluster** — the weight of the *problem*, not of the fix — on two factors, 1–3 each, multiplied for a score out of 9:

   | Factor | 1 | 2 | 3 |
   | --- | --- | --- | --- |
   | **Recurrence** — distinct issues the cluster's rows come from | 1 issue | 2–3 issues | 4+ issues |
   | **Cost** — the worst row in the cluster | Noted only; no rework, or a wording fix | An actioned finding of any severity, or a rework commit | A defect that reached `main`, or an actioned Blocker / Critical |

   Reviewer severity labels are generous, so a Blocker counts toward 3 only when the row shows it was actioned; a finding that merely reached an open PR is a 2.

   Then note the proposed fix's **Leverage** — memory entry or doc note, prompt or rule amendment, or deterministic gate (hook, CI check, test, script). Leverage is shown beside the score and does not enter it: the command chooses the fix, so scoring the fix would let it rank its own ambition.

   Sort by score; break ties by *mitigation failed* first, then Recurrence, then Cost. Show both factors, not only the product, so the invoker can dispute a single score.

7. **Present the list and ask.** Sort by score, highest first, and show at most 12 proposals; say how many were held back. Use this format:

   ```
   ## Tally
   Execution <n> · Plan-spec <n> · Codebase <n> · Environment <n> — <rows> rows across <files> records; <prs> PRs merged in the same window (<rate> rows per PR)

   ## Proposals

   ### 1. [Cluster name — the mistake, ≤12 words]  — score 6 (R3 × C2)
   Rows: 279#3, 280#8, 287#3
   Level: Execution
   Mitigation: [none | the existing rule or gate — and "failed: <row> postdates it" where one does]
   Fix: [one or two sentences — what changes, and in which file]
   Leverage: memory | prompt rule | gate
   Route: Implement | Issue — [one clause why]
   ```

   Then, briefly and only where non-empty: *Already addressed at HEAD*, *Record hygiene* (rows breaking the README's shape — a Finding over 30 words, missing source, unknown level), and any level that never populates.

   End with:

   > "Pick by number with a route each — `i` to implement now, `r` to raise an issue — e.g. `1i 3r 4r`. Or `none`."

   **Wait for the reply. Write nothing before it.** A pick without a route uses the recommended one.

8. **Apply the picks.**

   **Raise (`r`)** — one issue per pick. Write the body to `.claude/scratch/study-review-<n>.md` and create it with `gh issue create --title "<cluster name>" --body-file <path>` (a `--body` heredoc can stall on a confirmation prompt). The body is a brief, not a drafted issue:

   ```markdown
   ## Problem
   <the cluster name, and one or two sentences on the shared shape>

   ## Evidence
   - `docs/study/<N>.md` row <r>: <the Finding, trimmed>
   - …

   ## Proposed fix
   <the Fix line from step 7, and the file it touches>

   Score <s> (Recurrence <R> × Cost <C>) from `/study-review`; existing mitigation: <none, or the rule and whether it failed>.

   Brief only — run `/speckit.draftissue #<this issue>` to shape it before `/build`.
   ```

   **Implement (`i`)** — only when at least one pick is `i`:
   - If the branch is `main`, create `feature/study-review-<YYYY-MM-DD>` from an up-to-date `origin/main` first. Never commit to `main`.
   - Apply each fix, then sweep for what it touches — grep for the concept it adds or changes across `.claude/commands/`, `docs/` and `CLAUDE.md`, and reconcile any older rule it now contradicts.
   - Prompt, doc and config edits fall under the constitution's configuration carve-out. A fix that touches `src/` follows full RED-GREEN-REFACTOR — or, if that is more than a contained change, stop and offer to raise it as an issue instead.
   - Commit each fix separately, staging by explicit path. Message: `Apply study-review fix: <cluster name>`, with the cited rows in the body. No `Refs #…` unless the fix belongs to a real issue.
   - Leave any file dirty in the step-1 baseline exactly as it was, and never stage it.

9. **Stop.** Do not push, open a PR or merge. Never modify `docs/study/`.

---

## Final report

- The issues created, with their URLs.
- The fixes implemented: the commit for each, and the branch.
- Picks that were not carried out, and why.
- Pre-existing working-tree changes from the baseline, left untouched.
- If anything was implemented: "Run `/verify` on this branch, then open a PR when ready."
- If nothing was picked: "No fixes applied."
