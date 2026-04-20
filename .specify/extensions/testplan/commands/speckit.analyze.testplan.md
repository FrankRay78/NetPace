---
description: "Cross-check test-plan.md against spec.md and tasks.md, appending findings to the analyze report"
---

# Test Plan Cross-Check

Cross-check `test-plan.md` against `spec.md` and `tasks.md` for the current feature,
then append a **Test Plan Cross-Check** findings table to the analyze report.

This command runs automatically as an `after_analyze` hook.

---

## Step 1 — Locate the current feature

Read `.specify/feature.json` and extract `feature_directory`.
All source files are relative to the repo root: `{feature_directory}/spec.md`,
`{feature_directory}/test-plan.md`, `{feature_directory}/tasks.md`.

---

## Step 2 — Check test-plan.md exists

If `{feature_directory}/test-plan.md` does not exist:

Output this finding and stop:

```
## Test Plan Cross-Check

| Finding | Severity | Detail |
|---|---|---|
| test-plan.md missing | CRITICAL | No test plan exists for this feature. Run `/speckit.testplan` before implementing. |
```

---

## Step 3 — Extract artifacts

From `spec.md`: collect every `### Requirement:` header name (the text after the colon,
trimmed).

From `test-plan.md`: collect every `### Requirement:` header name and, under each, every
`#### Scenario:` header name.

From `tasks.md`: collect every task line (any line starting with `- [ ]` or `- [x]`).

---

## Step 4 — Run cross-checks

Perform all of the following checks and collect findings:

### A — Requirements coverage

For each requirement name in `spec.md`:
- If no matching `### Requirement:` appears in `test-plan.md`:
  → Finding: **Requirement not in test plan** | CRITICAL | Requirement name
- If a matching requirement exists but has zero `#### Scenario:` children:
  → Finding: **Requirement has no scenarios** | CRITICAL | Requirement name

### B — Requirement drift

For each `### Requirement:` name in `test-plan.md`:
- If no matching `### Requirement:` exists in `spec.md`:
  → Finding: **Requirement in test plan not found in spec** | WARNING | Requirement name — possible rename or deletion in spec.md

### C — Scenario label format

For each scenario in `test-plan.md`, verify it uses `#### Scenario:` heading (four `#`
marks). Any scenario that uses a different heading level or does not have the `Scenario:`
prefix:
  → Finding: **Malformed scenario label** | WARNING | Scenario text

### D — Scenario uniqueness

If any two `#### Scenario:` names in `test-plan.md` are identical (case-insensitive):
  → Finding: **Duplicate scenario name** | WARNING | Duplicate name — `/speckit.testchecklist` cannot distinguish between them

### E — Test plan completeness signal

If `test-plan.md` exists but contains no `#### Scenario:` entries at all:
  → Finding: **test-plan.md contains no scenarios** | CRITICAL | File exists but is empty of scenarios

---

## Step 5 — Output findings table

Append the following section as the final output of this hook invocation.

If **no findings** were produced:

```markdown
## Test Plan Cross-Check

| Finding | Severity | Detail |
|---|---|---|
| All checks passed | — | test-plan.md is consistent with spec.md and tasks.md |
```

If **findings exist**:

```markdown
## Test Plan Cross-Check

| Finding | Severity | Detail |
|---|---|---|
| {finding} | {severity} | {detail} |
| ... | ... | ... |
```

Severity legend: **CRITICAL** = blocks implementation, **WARNING** = should be reviewed.

---

## Rules

- Do NOT modify any source files
- Do NOT suggest fixes inline — list findings only
- Do NOT re-run `/speckit.analyze`; this command produces supplementary output only
- Requirement name matching is **exact** (trimmed, case-sensitive) — the same rule
  `/speckit.testchecklist` applies
- If `spec.md` or `tasks.md` does not exist, skip the checks that depend on it and note
  the missing file as a WARNING in the findings table
