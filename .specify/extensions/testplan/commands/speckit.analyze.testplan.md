---
description: "Cross-check test-plan.md against spec.md `**Scenario:**` labels, appending findings to the analyze report"
---

# Test Plan Cross-Check

Cross-check `test-plan.md` `#### Scenario:` headings against `spec.md` `**Scenario: [name]**`
labels for the current feature, then append a **Test Plan Cross-Check** findings table to
the analyze report.

This command runs automatically as an `after_analyze` hook. It enforces AC-to-Test
Traceability: every spec.md `**Scenario:**` label MUST appear as a `#### Scenario:`
heading in test-plan.md with an exact name match (case and punctuation included).

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

From `spec.md`: collect every `**Scenario: [name]**` label across all `### User Story N`
sections. The name is the text between `**Scenario: ` and the trailing `**`, trimmed.

From `test-plan.md`: collect every `#### Scenario: [name]` heading. The name is the text
after `#### Scenario: `, trimmed. Also capture the body of each scenario (the lines
between this heading and the next `####`/`###`/EOF) so check F can verify it.

---

## Step 4 — Run cross-checks

Perform all of the following checks and collect findings:

### A — Scenario coverage in test-plan.md

For each scenario name in `spec.md`:
- If no matching `#### Scenario:` heading appears in `test-plan.md` (exact match,
  case-sensitive after trim):
  → Finding: **Scenario in spec missing from test-plan** | CRITICAL | Scenario name —
  every spec scenario must appear in test-plan.md to maintain traceability

### B — Scenario drift in test-plan.md

For each `#### Scenario:` name in `test-plan.md`:
- If no matching `**Scenario:**` label appears in `spec.md`:
  → Finding: **Scenario in test-plan not found in spec** | WARNING | Scenario name —
  possible rename or deletion in spec.md; reconcile by re-running `/speckit.testplan`

### C — Case/whitespace-only mismatches

For each spec.md scenario name with no exact match in test-plan.md, check whether a
case-insensitive trim-collapsed match exists. If yes:
  → Finding: **Scenario name mismatch (case or whitespace)** | CRITICAL | Spec name vs
  test-plan name — names must match character-for-character (the traceability key is exact)

### D — Duplicate scenario names in spec.md

If any two `**Scenario:**` labels in `spec.md` are identical (case-insensitive after trim):
  → Finding: **Duplicate scenario name in spec** | CRITICAL | Duplicate name — names
  must be unique to act as the traceability key

Note: the downstream match key (used by checks A–C and by `/speckit.testchecklist`) is
**case-sensitive** after trim. The case-insensitive comparison here is deliberately
broader so near-duplicates (`Login OK` vs `login ok`) are caught as drift even though
they would not collide under exact matching.

### E — Duplicate scenario names in test-plan.md

If any two `#### Scenario:` names in `test-plan.md` are identical (case-insensitive after
trim):
  → Finding: **Duplicate scenario name in test-plan** | WARNING | Duplicate name —
  `/speckit.testchecklist` cannot distinguish between them (same case-insensitive
  rationale as check D)

### F — Scenario label and body format in test-plan.md

For each scenario in `test-plan.md`:

1. Verify the heading uses exactly `#### Scenario:` (four `#` marks). Any heading that
   uses a different depth (`###`, `#####`) or omits the `Scenario:` prefix:
   → Finding: **Malformed scenario heading in test-plan** | WARNING | Scenario text

2. Verify the captured body (Step 3) contains at least one `**WHEN**` line and at least
   one `**THEN**` line. A scenario with a heading but no `**WHEN**`/`**THEN**` body:
   → Finding: **Empty or incomplete scenario body in test-plan** | CRITICAL | Scenario name —
   a scenario without WHEN/THEN cannot drive a test

### G — Test plan completeness signal

If `test-plan.md` exists but contains no `#### Scenario:` entries at all:
  → Finding: **test-plan.md contains no scenarios** | CRITICAL | File exists but is
  empty of scenarios

If `spec.md` exists but contains no `**Scenario:**` labels at all:
  → Finding: **spec.md contains no Scenario labels** | CRITICAL | every Acceptance
  Scenario must carry a `**Scenario: [name]**` label to be traceable

---

## Step 5 — Output findings table

Append the following section as the final output of this hook invocation.

If **no findings** were produced:

```markdown
## Test Plan Cross-Check

| Finding | Severity | Detail |
|---|---|---|
| All checks passed | — | spec.md `**Scenario:**` labels and test-plan.md `#### Scenario:` headings match (AC-to-Test Traceability holds) |
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
- Scenario name matching is **exact** (case-sensitive after trim) — the same rule
  `/speckit.testchecklist` applies to test code `// SCENARIO:` comments
- If `spec.md` does not exist, this is a CRITICAL finding (cannot perform the
  cross-check at all); skip the dependent steps
