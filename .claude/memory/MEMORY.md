
<!-- Index of repo-tracked memory entries. One line per entry: `- [Title](file.md) — one-line hook`. No frontmatter on this file. -->

- [Run speckit git hooks via their bash script](feedback_speckit_hooks.md) — disable-model-invocation: true blocks Skill; call the bash script directly
- [Speckit testplan: after_analyze hook has no bash script — execute inline](feedback_speckit_testplan_hook.md) — read the .md and follow its steps before ending the response
- [Spec-kit task lists prescribe tactics — treat them as suggestions](feedback_speckit_implementation_prescriptions.md) — distinguish prescribed *outcome* from prescribed *tactic*; prefer the simpler equivalent
- [Challenge speculative numeric tolerances in test plans](feedback_speccheck_numeric_tolerance.md) — exact equality by default; only add tolerance when the operation actually introduces FP error
- [Prompts favour locality over DRY](feedback_prompts_locality_over_dry.md) — inline short rules in each slash-command prompt; skip canonical-section + cross-refs and defensive specs for cases the generator can't produce
- [Codebase must not reference specs/ paths](feedback_no_spec_references.md) — specs are deleted post-merge; any `specs/<NNN>-…` link in source/tests/docs becomes a dead reference
- [Docs describe current codebase only — no forward references](feedback_docs_no_forward_references.md) — push back on task plans that mandate doc sections about unimplemented features tracked only by open issues
- [After adding or removing a rule, sweep for what it touches](feedback_grep_after_simplifying.md) — grep for a removed concept repo-wide; reconcile a new prompt rule with older rules and re-run paths
- [Don't introduce column-aligned whitespace in code](feedback_no_column_alignment.md) — single-space tokens; aligned blocks cause diff churn and break outside the editor
- [Scratch and staging files belong in .claude/scratch/](feedback_scratch_file_location.md) — gitignored; not /tmp, not ~/.claude/, not a top-level .scratch/
- [Provider-agnostic types must not depend on concrete providers](project_dependency_direction.md) — Profile, SpeedUnit, etc. never reach forward into provider types; bridge lives on the provider side
- [NetPace CLI feature issues must scope user-facing docs from the start](feedback_cli_feature_doc_scope.md) — first draft must include the CLAUDE.md CLI-option docs set (no CHANGELOG)
- [Verify-snapshot tests count as coverage in NetPace.Console.Tests](feedback_console_output_snapshot_coverage.md) — check Expectations/*.verified.txt before reporting an output mode as untested
- [Speckit file guard is Edit-only by design](speckit_file_guard.md) — one `Edit(path)` deny rule per protected path; `Edit` covers `Write`, so parallel `Write`/`MultiEdit` rules only add startup warnings
- [Spec-kit upgrade procedure](speckit_upgrade_procedure.md) — stock github/spec-kit via `specify` CLI; `init --here --force --integration claude --script sh` is additive/hash-guarded
- [Read source before designing fixes](feedback_read_source_before_designing.md) — read the source before designing a fix; verify a handed-down issue/spec diagnosis against HEAD before implementing
- [Trust only a fresh, rebuilt, unpiped test run](feedback_trusting_a_test_run.md) — re-run after the last edit, no stale `--no-build`, never pipe a run you trust; read Passed!/Failed!
- [Decompose quantified claims — no flattering multipliers](feedback_plain_language_decisions.md) — name what an estimate is made of instead of a headline multiplier
- [Be decisive when the evidence has already earned it](feedback_decisiveness_over_hedging.md) — state the verdict the gates already support; build within a decision already made rather than re-litigating it
- [OoklaSpeedtest latency-margin test is flaky under load](project_flaky_latency_margin_test.md) — wall-clock ±25% assertion; distinct from the ProfileXmlDocTests FileShare flake
- [When the guard outgrows the fix, surface it as a decision](feedback_guard_outgrows_the_fix.md) — verification scaffolding bigger than the change means the wrong mechanism; ask before building
