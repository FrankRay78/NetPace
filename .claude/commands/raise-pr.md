---
description: Raise a PR for the current feature branch — cleans up the spec folder, composes a PR body sized to the change, and requests an @claude review.
---

Read `CLAUDE.md` for project context before proceeding.

## User Input

```text
$ARGUMENTS
```

Optionally a GitHub issue number — bare (`248`), hashed (`#248`), or a full issue URL — naming the issue this PR closes. Empty is the normal case and is **not** a prompt: step 6 infers the issue from the branch instead. `/raise-pr` never asks the invoker for anything, so it stays composable by `/ship` with no interactive gate.

## Steps

1. **Get branch name**: Run `git rev-parse --abbrev-ref HEAD`. If the result is `main`, stop immediately and output: "Run /raise-pr from a feature branch, not main."

2. **Check for commits**: Run `git log main..HEAD --oneline`. If the output is empty, stop immediately and output: "No commits found on this branch compared to main — nothing to raise a PR for."

3. **Detect spec folder for this branch**: Run the spec-kit prerequisites helper if present in the repo:
   - `bash .specify/scripts/bash/check-prerequisites.sh --json --paths-only`

   If the script does not exist, or the command exits non-zero (non-feature branch), skip to step 5.

   Otherwise parse `FEATURE_DIR` from the JSON output. If `FEATURE_DIR` does not exist on disk, skip to step 5. Otherwise keep it for step 4.

4. **Delete spec folder**: Specs are deleted before merge so they don't accumulate on `main`, so remove `<FEATURE_DIR>` and commit the removal without prompting (this keeps `/raise-pr` composable by `/ship` with no interactive gate):
   - Run `git rm -r <FEATURE_DIR>`
   - Clear the spec-kit feature pointer so it doesn't dangle at a deleted folder on `main`: if `.specify/feature.json` exists and its `feature_directory` resolves to the same folder as `<FEATURE_DIR>` (compare as repo-relative paths — `FEATURE_DIR` from `--paths-only` is absolute, while the JSON stores a repo-relative path like `specs/NNN-…`), reset it to `{"feature_directory": ""}` and `git add .specify/feature.json`. (A stale pointer makes `check-prerequisites.sh` hard-fail and lets `setup-plan.sh` silently recreate the deleted folder.)
   - Run `git commit -m "chore: remove <FEATURE_DIR>"`

5. **Infer PR title**: Take the branch name from step 1, strip any leading path segment (`feat/`, `fix/`, `chore/`, `docs/`, etc.), strip any leading `NNN-` numeric prefix on the remaining segment, replace hyphens with spaces, and apply title case. Examples: `004-raise-pr-command` → `Raise PR Command`; `chore/speckit-docs-tidy` → `Speckit Docs Tidy`.

6. **Compose PR body**: Write a description sized to the change. **Do not follow a fixed template.** Decide which sections earn their place based on what's actually in this PR.

   **First, settle the closing keyword.** A merged PR must close the issue it implemented without the invoker having to remember to ask for it, so decide this every time — not only when someone thinks to mention an issue:

   - **An issue number supplied by the invoker wins.** Take it from the User Input above (bare, `#N`, or an issue URL) and use it directly — no branch inference, no second-guessing.
   - **Otherwise infer a candidate from the branch name.** `feature/<N>-<slug>` is what `/build` produces, so the leading number on the segment after the prefix is the candidate.
   - **Verify the candidate before using it — never trust the parse.** Run `gh issue view <N> --json number,state,title,url`. Use `Closes #<N>` only if all three hold: the lookup succeeded, `state` is `OPEN`, and `url` is an `/issues/<N>` path. Otherwise add **no** closing keyword and carry on composing the body.
   - Verifying is the point of the rule, not ceremony around it: spec-kit branches carry a *sequence* number, not an issue number — `004-raise-pr-command` is the very branch step 5 strips `004-` from — so an unverified parse would emit `Closes #4` and silently auto-close an unrelated issue the moment the PR merged. That is a worse failure than the missing link this rule exists to fix, and nobody notices it until an issue is mysteriously closed.
   - The `url` check is not belt-and-braces. GitHub numbers issues and pull requests from one sequence, and `gh issue view <N>` resolves a *pull request* at that number rather than failing: on this repo `gh issue view 4` returns `{"number":4,"state":"MERGED","url":".../pull/4"}`. A state-only check therefore passes for an open PR and would emit a `Closes` aimed at a pull request. The `/issues/` path is what separates the two.
   - The keyword goes on its own line in **Related**. The PR body is the *only* place a closing keyword belongs — never a commit message, which would close the issue as soon as it reached `main`, ahead of review.
   - Whichever way it lands, step 10 reports it.

   **Guiding principle — don't duplicate things a reviewer can get elsewhere.** GitHub already shows:
   - the list of changed files and their diffs
   - the commit list and commit messages
   - line-count statistics, additions/deletions
   - the author, base/head branches, and CI status

   Don't repeat any of that in the body. Use the body for things a reviewer *can't* easily get from the code or git history.

   **Sections to consider** (include each only if it adds value for this PR):

   - **Why** — motivation or context that isn't already obvious from commit messages. Skip for trivial PRs where the title is self-explanatory.
   - **What changes** — a short summary of observable changes. For single-theme PRs, 1–3 bullets. For multi-theme PRs, group by theme rather than by file. Describe *what* changed at a behaviour level, not which files were touched.
   - **Non-obvious things a reviewer should know** — caveats, deliberate trade-offs, hidden invariants, anything in the diff that looks weird but is intentional. Omit if there are none.
   - **How to verify** — a markdown checklist of concrete actions a reviewer can perform. Derive items from what actually changed. Omit for pure-documentation PRs where there's nothing functional to exercise.
   - **Related** — the `Closes #<N>` line settled above, when one was settled, plus links to prior PRs, related issues, or specs that give context. Omit only if there is neither.

   A tiny PR may collapse to two sections. A sprawling PR may use all five. Match the size of the body to the size of the change.

7. **Push branch**: Run `git push -u origin <branch>`. Ensures the spec-deletion commit (if any) is included in the PR.

8. **Create PR**: Run `gh pr create --title "<inferred title>" --body "<pr body>"` with the title from step 5 and the body from step 6. Capture the PR URL from the output. Use whatever multi-line string quoting your shell needs (bash heredoc, PowerShell here-string, etc.).

9. **Request @claude review**: Run `gh pr comment <PR URL> --body "<review prompt>"` with this body (same shell-quoting rules as step 8):

   ```
   @claude Review this pull request. Analyse the code changes and provide feedback covering:
   - Bugs or correctness issues
   - Security concerns (including any flagged by static analysis)
   - Adherence to the project conventions in CLAUDE.md
   - Test coverage — do the tests adequately cover the new behaviour?
   - Any spec/test-plan mismatches
   End the review with a recommendation whether to merge the PR, and if not, what you suggest needs addressing first.
   ```

10. **Output result**: Print the PR URL. If step 4 deleted a spec folder, also print `Deleted spec folder: <FEATURE_DIR>`. Always print what step 6 settled — `Closes #<N> — <issue title>`, or, when it settled nothing, that fact and the reason (`No closing keyword: branch carries no issue number`; `No closing keyword: #4 is a pull request, not an open issue`) — so the invoker can see from the output alone whether merging this PR will close anything, and what.
