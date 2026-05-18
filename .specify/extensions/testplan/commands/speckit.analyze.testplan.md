---
description: "Cross-check test-plan.md against spec.md and verify each scenario is implementable in the project's test tooling, appending findings to the analyze report"
---

# Test Plan Cross-Check

Cross-check `test-plan.md` `#### Scenario:` headings against `spec.md` `**Scenario: [name]**` labels for the current feature, verify each scenario is implementable in the project's actual test tooling, then append a **Test Plan Cross-Check** findings table to the analyze report.

This command runs automatically as an `after_analyze` hook. It enforces two contracts:

1. **AC-to-Test Traceability** — every spec.md `**Scenario:**` label MUST appear as a `#### Scenario:` heading in test-plan.md with an exact name match (case and punctuation included). Checks A–G in Step 4.
2. **Planning-stage test implementability** — every scenario in test-plan.md MUST be implementable as an automated test using the tooling actually present in the repository's manifests. Check H in Step 6. Scenarios that require tooling not in the project (e.g. browser automation when no Playwright is installed) MUST be resolved at the planning stage by editing test-plan.md, not by writing skipped / `NotImplementedException` / `Decision=Pending` stubs in the codebase.

---

## Step 1 — Locate the current feature

Read `.specify/feature.json` and extract `feature_directory`. All source files are relative to the repo root: `{feature_directory}/spec.md`, `{feature_directory}/test-plan.md`, `{feature_directory}/tasks.md`.

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

From `spec.md`: collect every `**Scenario: [name]**` label across all `### User Story N` sections. The name is the text between `**Scenario: ` and the trailing `**`, trimmed.

From `test-plan.md`: collect every `#### Scenario: [name]` heading. The name is the text after `#### Scenario: `, trimmed. Also capture the body of each scenario (the lines between this heading and the next `####`/`###`/EOF) so check F can verify it.

---

## Step 4 — Run cross-checks

Perform all of the following checks and collect findings:

### A — Scenario coverage in test-plan.md

For each scenario name in `spec.md`:
- If no matching `#### Scenario:` heading appears in `test-plan.md` (exact match, case-sensitive after trim): → Finding: **Scenario in spec missing from test-plan** | CRITICAL | Scenario name — every spec scenario must appear in test-plan.md to maintain traceability

### B — Scenario drift in test-plan.md

For each `#### Scenario:` name in `test-plan.md`:
- If no matching `**Scenario:**` label appears in `spec.md`: → Finding: **Scenario in test-plan not found in spec** | WARNING | Scenario name — possible rename or deletion in spec.md; reconcile by re-running `/speckit.testplan`

### C — Case/whitespace-only mismatches

For each spec.md scenario name with no exact match in test-plan.md, check whether a case-insensitive trim-collapsed match exists. If yes: → Finding: **Scenario name mismatch (case or whitespace)** | CRITICAL | Spec name vs test-plan name — names must match character-for-character (the traceability key is exact)

### D — Duplicate scenario names in spec.md

If any two `**Scenario:**` labels in `spec.md` are identical (case-insensitive after trim): → Finding: **Duplicate scenario name in spec** | CRITICAL | Duplicate name — names must be unique to act as the traceability key

Note: the downstream match key (used by checks A–C and by `/speckit.testchecklist`) is **case-sensitive** after trim. The case-insensitive comparison here is deliberately broader so near-duplicates (`Login OK` vs `login ok`) are caught as drift even though they would not collide under exact matching.

### E — Duplicate scenario names in test-plan.md

If any two `#### Scenario:` names in `test-plan.md` are identical (case-insensitive after trim): → Finding: **Duplicate scenario name in test-plan** | WARNING | Duplicate name — `/speckit.testchecklist` cannot distinguish between them (same case-insensitive rationale as check D)

### F — Scenario label and body format in test-plan.md

For each scenario in `test-plan.md`:

1. Verify the heading uses exactly `#### Scenario:` (four `#` marks). Any heading that uses a different depth (`###`, `#####`) or omits the `Scenario:` prefix: → Finding: **Malformed scenario heading in test-plan** | WARNING | Scenario text

2. Verify the captured body (Step 3) contains at least one `**WHEN**` line and at least one `**THEN**` line. A scenario with a heading but no `**WHEN**`/`**THEN**` body: → Finding: **Empty or incomplete scenario body in test-plan** | CRITICAL | Scenario name — a scenario without WHEN/THEN cannot drive a test

### G — Test plan completeness signal

If `test-plan.md` exists but contains no `#### Scenario:` entries at all: → Finding: **test-plan.md contains no scenarios** | CRITICAL | File exists but is empty of scenarios

If `spec.md` exists but contains no `**Scenario:**` labels at all: → Finding: **spec.md contains no Scenario labels** | CRITICAL | every Acceptance Scenario must carry a `**Scenario: [name]**` label to be traceable

---

## Step 5 — Detect project test tooling

Build a **tooling fingerprint** by scanning manifest files for test-related dependencies. This fingerprint feeds the implementability check in Step 6. The detection is best-effort and intentionally repo-agnostic — the fingerprint is just the literal package/dependency names found, passed verbatim into the per-scenario prompt.

Scan the repo root and immediate subdirectories (e.g. `src/`, `tests/`, `apps/`, `packages/`) for the following manifests. Do NOT descend into `node_modules/`, `bin/`, `obj/`, `target/`, `dist/`, `build/`, `.venv/`, or `vendor/`.

| Manifest | Extract |
|---|---|
| `*.csproj`, `*.fsproj`, `*.vbproj` | `<PackageReference Include="X" />` values |
| `package.json` | `dependencies` + `devDependencies` keys |
| `pyproject.toml` | `[tool.poetry.dependencies]`, `[project.dependencies]`, `[tool.poetry.group.*.dependencies]` keys |
| `requirements*.txt`, `dev-requirements*.txt` | one package per line, before any version specifier |
| `Cargo.toml` | `[dependencies]`, `[dev-dependencies]` keys |
| `go.mod` | `require` block module paths |
| `Gemfile`, `Gemfile.lock` | `gem` entries |
| `composer.json` | `require` + `require-dev` keys |

Deduplicate the collected package names. The fingerprint is this deduplicated list rendered as a plain comma-separated string, capped at 8 KB (truncate by dropping the tail if exceeded — common test packages typically appear first because they live in test-project manifests scanned early).

If no manifests are found, the fingerprint is the literal string `(none)` and Step 6 will treat every scenario as `NOT_IMPLEMENTABLE` with reason "no test tooling detected in any project manifest". That is the correct signal for a repo with no test infrastructure.

---

## Step 6 — Test implementability check (H)

For each `#### Scenario:` body captured in Step 3, classify whether the scenario can be implemented as an automated test under the tooling fingerprint from Step 5. The classification is model-driven — no hard-coded category map. The vocabulary is exactly two verdicts.

For each scenario, issue this prompt to the model and capture the response:

> Project test tooling fingerprint:
> `{fingerprint from Step 5}`
>
> Acceptance scenario (verbatim from test-plan.md):
> ```
> {scenario heading and full WHEN/THEN/AND body}
> ```
>
> Can this scenario be implemented as an automated test using only the tooling listed in the fingerprint? Answer with one of:
>
> - `IMPLEMENTABLE` — every assertion in the scenario can be verified deterministically with the listed tooling.
> - `NOT_IMPLEMENTABLE` — at least one assertion requires capability not present in the fingerprint (e.g. browser automation, JS test runner, runtime DOM observation, wall-clock timing of UI animations, cross-screen JS state observation).
>
> Then on a new line, output one sentence stating the specific blocking assertion and the missing capability. No remediation, no recommendations — finding only.

Parse the response: first non-empty line must start with `IMPLEMENTABLE` or `NOT_IMPLEMENTABLE`; subsequent text is the reason. If parsing fails, treat as `NOT_IMPLEMENTABLE` with reason "audit could not classify (malformed response)".

Collect findings:

- For each `NOT_IMPLEMENTABLE` scenario: → Finding: **Scenario not implementable in project tooling** | CRITICAL | `{scenario name} — {one-sentence reason}`

- `IMPLEMENTABLE` scenarios emit no finding (consistent with Step 4's silent-pass convention).

The intent of this check is to prevent the planning stage from producing scenarios that will become skipped, deferred, or `NotImplementedException` stubs in the codebase. Resolution of a `NOT_IMPLEMENTABLE` finding is to edit `test-plan.md` — either remove the scenario or rewrite it as a testable proxy. Adding stubs to the codebase is not a legal resolution.

---

## Step 7 — Output findings table

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
- Scenario name matching is **exact** (case-sensitive after trim) — the same rule `/speckit.testchecklist` applies to test code `// SCENARIO:` comments
- If `spec.md` does not exist, this is a CRITICAL finding (cannot perform the cross-check at all); skip the dependent steps
- Step 6 (check H) does NOT recommend tooling additions or scenario rewrites — it reports only. Resolving a `NOT_IMPLEMENTABLE` finding is a planning-stage decision (edit `test-plan.md`), not an implementation-stage workaround (add a stub or skip)
- Step 6's verdict vocabulary is exactly two values (`IMPLEMENTABLE` / `NOT_IMPLEMENTABLE`). Do not introduce additional verdicts; concerns about test *value* or *tautology* belong to code review or `/speckit.testchecklist`, not here
