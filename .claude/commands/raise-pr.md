---
description: Raise a PR for the current feature branch — cleans up the spec folder, composes a PR body sized to the change, and requests an @claude review.
---

Read `CLAUDE.md` for project context before proceeding.

## User Input

```text
$ARGUMENTS
```

Optionally a GitHub issue number — bare (`248`), hashed (`#248`), or a full issue URL — naming the issue this PR closes. Empty is the normal case and is **not** a prompt: step 6 infers a candidate from the branch instead. `/raise-pr` never asks the invoker for anything, so it stays composable by `/ship` with no interactive gate.

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

   - **Find a candidate.** A number supplied by the invoker wins over anything inferred. Otherwise take the leading number off a branch matching the `<prefix>/<N>-<slug>` shape `/build` produces, so `feature/248-pr-closing-keyword` yields `248`. Two shapes yield **no candidate**: a branch with no leading number on that segment (`feature/build-command`), and a bare `NNN-<slug>` spec-kit branch (`004-raise-pr-command`) — spec-kit numbers are *sequence* numbers that restart from `001`, so parsing one and trusting the check below to reject it is strictly worse than never parsing it. Never hunt for a number elsewhere in the name: `feature/net10-upgrade` must not yield `10`. Normalise whatever you found to a bare number — `248`, `#248` and an issue URL all reduce to `248` (strip `#` and any leading zeros, take the trailing number from a URL) — and use that as `<N>` in every check and message below.
   - **A candidate is only ever a candidate.** However it arrived — typed by the invoker or parsed from the branch — it is unverified until `gh` confirms it. The invoker's number wins the *sourcing*, never the *checking*: a hand-typed digit is if anything easier to get wrong than a parse, so `/raise-pr 247` (a PR number) and `/raise-pr 24` (a slip for `248`) must not sail through.
   - **Verify it.** Resolve this repo once with `gh repo view --json nameWithOwner -q .nameWithOwner`, then run `gh issue view <N> --json number,state,title,url -R <owner>/<repo>`. Use `Closes #<N>` only if the command exits 0, `state` is `OPEN`, and `url` is exactly `https://github.com/<owner>/<repo>/issues/<N>`.
   - That one URL equality is doing three jobs, each of which otherwise produces a confident `Closes` aimed at the wrong thing. GitHub numbers issues and pull requests from a single sequence, so `gh issue view` resolves a *pull request* at that number instead of failing — here `gh issue view 4` returns `{"state":"MERGED","url":".../pull/4"}`. `gh issue view <issue URL>` resolves against *that URL's* repo, so a pasted `github.com/cli/cli/issues/14353` verifies clean and would emit a `Closes #14353` that means something unrelated in this repo. And a transferred issue redirects to a different number. Comparing the whole URL rejects all three at once.
   - **Report one of three outcomes — never fold the last two together.**
     - **Linked** — every condition held. Put `Closes #<N>` on its own line in **Related**.
     - **No issue** — the lookup settled the question. Either it exited 0 and the answer disqualifies the number (a pull request, an issue already `CLOSED`, or an issue in another repo), or it exited non-zero saying `Could not resolve to an issue or pull request`, which is GitHub definitively answering that nothing exists at `<N>`. Add no closing keyword. When it is a real but closed issue in *this* repo, still record `Refs #<N>` in **Related**: dropping the closing keyword is the safety property, dropping every trace of the link is collateral damage.
     - **Unverified** — the lookup could not answer at all: expired auth, no network, rate limit, `gh` missing. Add no closing keyword, and say the check could not run, quoting `gh`'s stderr. Read the stderr, not the exit code, to tell this from the case above — both exit non-zero, but `HTTP 401: Bad credentials` is a broken check while `Could not resolve to an issue or pull request` is an answer. Reporting a fixable auth failure as "no such issue" sends the invoker looking in the wrong place.
   - A non-zero `gh issue view` here is **not** a `/ship` step failure despite that command's global stop-on-failure rule — it is an abstain, reported rather than fatal. Compose the rest of the body and carry on.
   - What this proves is bounded, and worth knowing: that `<N>` is an open issue in this repo, never that it is *this branch's* issue, and it can change state between here and merge. Restricting inference to the `/build` shape is what keeps that gap narrow.
   - The PR body is the *only* place a closing keyword belongs — never a commit message, which would close the issue as soon as it reached `main`, ahead of review.
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
   - **Related** — the `Closes #<N>` (or `Refs #<N>`) line settled above, plus links to prior PRs, related issues, or specs that give context. This section always survives when step 6 settled a keyword, whatever the PR's size.

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

10. **Output result**: Print the PR URL. If step 4 deleted a spec folder, also print `Deleted spec folder: <FEATURE_DIR>`. Then print step 6's outcome in whichever of its three forms applies — `Closes #<N> — <issue title>`; `No closing keyword: <reason>` (the branch carries no issue number; `#<N>` is a pull request; `#<N>` is closed); or `Closing keyword unverified: <gh stderr>` — so the invoker can see from the output alone what merging this PR will close, if anything.
