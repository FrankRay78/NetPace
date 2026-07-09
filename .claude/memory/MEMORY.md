<!-- Index of repo-tracked memory entries. One line per entry: `- [Title](file.md) — one-line hook`. No frontmatter on this file. -->

- [Speckit git hooks: run auto-commit.sh directly, not via Skill tool](feedback_speckit_hooks.md) — disable-model-invocation: true blocks Skill; call the bash script directly
- [Speckit testplan: after_analyze hook has no bash script — execute inline](feedback_speckit_testplan_hook.md) — read the .md and follow its steps before ending the response
- [Spec-kit task lists prescribe tactics — treat them as suggestions](feedback_speckit_implementation_prescriptions.md) — distinguish prescribed *outcome* from prescribed *tactic*; prefer the simpler equivalent
- [Challenge speculative numeric tolerances in test plans](feedback_speccheck_numeric_tolerance.md) — exact equality by default; only add tolerance when the operation actually introduces FP error
- [Prompts favour locality over DRY](feedback_prompts_locality_over_dry.md) — inline short rules in each slash-command prompt; skip canonical-section + cross-refs and defensive specs for cases the generator can't produce
- [Codebase must not reference specs/ paths](feedback_no_spec_references.md) — specs are deleted post-merge; any `specs/<NNN>-…` link in source/tests/docs becomes a dead reference
- [Docs describe current codebase only — no forward references](feedback_docs_no_forward_references.md) — push back on task plans that mandate doc sections about unimplemented features tracked only by open issues
- [After simplifying, grep the whole repo for the removed concept](feedback_grep_after_simplifying.md) — diff misses stale comments and docs in files you didn't directly touch
- [Don't introduce column-aligned whitespace in code](feedback_no_column_alignment.md) — single-space tokens; aligned blocks cause diff churn and break outside the editor
- [Scratch and staging files belong in .claude/scratch/](feedback_scratch_file_location.md) — gitignored; not /tmp, not ~/.claude/, not a top-level .scratch/
- [Provider-agnostic types must not depend on concrete providers](project_dependency_direction.md) — Profile, SpeedUnit, etc. never reach forward into provider types; bridge lives on the provider side
- [NetPace CLI feature issues must scope user-facing docs from the start](feedback_cli_feature_doc_scope.md) — first draft must include README --help, USER_GUIDE, design-doc cross-refs (no CHANGELOG — release notes are GitHub-auto-generated)
- [Release-pipeline changes must update docs/RELEASING.md](feedback_release_pipeline_doc.md) — release matrix, runner-per-RID, naming convention, smoke-test contract live there; out-of-sync = future RID work costs extra
- [Verify-snapshot tests count as coverage in NetPace.Console.Tests](feedback_console_output_snapshot_coverage.md) — check Expectations/*.verified.txt before reporting an output mode as untested
