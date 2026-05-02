<!-- Index of repo-tracked memory entries. One line per entry: `- [Title](file.md) — one-line hook`. No frontmatter on this file. -->

- [Provider-agnostic types must not depend on concrete providers](project_dependency_direction.md) — Profile, SpeedUnit, etc. never reach forward into provider types; bridge lives on the provider side
- [Scratch and staging files belong in the repo](feedback_scratch_file_location.md) — write temp drafts and memory into the repo, not into ~/.claude/
- [NetPace CLI feature issues must scope user-facing docs from the start](feedback_cli_feature_doc_scope.md) — first draft must include README --help, USER_GUIDE, design-doc cross-refs (no CHANGELOG — release notes are GitHub-auto-generated)
- [Release-pipeline changes must update docs/RELEASING.md](feedback_release_pipeline_doc.md) — release matrix, runner-per-RID, naming convention, smoke-test contract live there; out-of-sync = future RID work costs extra
- [Verify-snapshot tests count as coverage in NetPace.Console.Tests](feedback_console_output_snapshot_coverage.md) — check Expectations/*.verified.txt before reporting an output mode as untested
- [Challenge speculative numeric tolerances in test plans](feedback_speccheck_numeric_tolerance.md) — exact equality by default; only add tolerance when the operation actually introduces FP error
- [Spec-kit task lists prescribe tactics — treat them as suggestions](feedback_speckit_implementation_prescriptions.md) — distinguish prescribed *outcome* from prescribed *tactic*; prefer the simpler equivalent
- [Don't introduce column-aligned whitespace in code](feedback_no_column_alignment.md) — single-space tokens; aligned blocks cause diff churn and break outside the editor
