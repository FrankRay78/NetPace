---
description: Raise a PR for the current feature branch with auto-detected spec and CIR links.
---

Read `CLAUDE.md` and `docs/conventions/*.md` for project context before proceeding.

## Steps

1. **Get branch name**: Run `git rev-parse --abbrev-ref HEAD`. If the result is `main`, stop immediately and output: "Run /raise-pr from a feature branch, not main."

2. **Check for commits**: Run `git log main..HEAD --oneline`. If the output is empty, stop immediately and output: "No commits found on this branch compared to main — nothing to raise a PR for."

3. **Get all changed files**: Run `git diff main...HEAD --name-only` to list every file changed on this branch.

4. **Get added files**: Run `git diff main...HEAD --diff-filter=A --name-only` to list only files new to this branch.

5. **Extract artifacts** from the added files (step 4):
   - Spec folders: find any matching `specs/*/spec.md` and extract the parent folder (e.g. `specs/004-raise-pr-command`)
   - CIR files: find any matching `docs/change-intent-records/*.md` and use the full path (e.g. `docs/change-intent-records/006-slug.md`)

6. **Infer PR title**: Take the branch name from step 1, strip any leading `NNN-` numeric prefix, replace hyphens with spaces, and apply title case. Examples: `004-raise-pr-command` → `Raise PR Command`; `add-glider-pattern` → `Add Glider Pattern`.

7. **Build PR body**: Read `.github/pull_request_template.md` to get the section structure. Pre-fill as follows:
   - **Summary**: Write 2–3 sentences covering what changed, why it was needed, and the approach taken — enough context for an independent reviewer without them needing to read the code first. Draw from the changed files (step 3) and commit list (step 2).
   - **Spec**: Markdown link to detected spec folder(s), or `N/A` if none detected
   - **Changed Files**: All files from step 3, one per line
   - **New Artifacts**: For each detected spec folder or CIR file, one markdown link per line (e.g. `- Spec: [specs/004-raise-pr-command](specs/004-raise-pr-command)`). Omit the section entirely if no artifacts were detected.

8. **Create PR**: Run `gh pr create --title "<inferred title>" --body "<pr body>"` using the values from steps 6 and 7.

9. **Output result**: Print the PR URL returned by `gh pr create`. If any spec folders or CIR files were detected in step 5, also print a single line: `Detected: <comma-separated list>`. Omit the `Detected:` line entirely if no spec folders or CIR files were found.
