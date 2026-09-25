# Every `*.tests.sh` runs in CI, discovered by name

**Intent:** Close the gap where the repo's four shell test matrices — `green-gate.tests.sh`, `no-skipped-tests.tests.sh`, `traceability-gate.tests.sh`, `chain.tests.sh` — were verified only when someone remembered to run them. None is a `dotnet test` project, so `dotnet.yml` never saw them, and Constitution Principle I asks that a tool which can run in CI be gated there rather than verified once by hand.

**Behaviour:**
- Given a pull request on which any committed `*.tests.sh` exits non-zero, when CI runs, then the job fails and the failing script is named.
- Given a `*.tests.sh` committed anywhere in the repo, when CI runs, then it is executed without anyone having edited a workflow.
- Given a pull request on which the .NET build or test job is red, when CI runs, then the shell matrices still report their own verdict.
- Given a pull request on which every matrix passes, when CI runs, then this gate does not fail it.

**Constraints:**
- The matrices need only bash, jq and git, all preinstalled on `ubuntu-latest`. They stand up their own throwaway sandboxes and, where they commit, configure their own git identity, so the runner needs no setup step.
- The hooks' own `--check` modes stay out of CI. They are hook-only by design; what is gated here is their test matrices, not the gates themselves.

**Decisions:**

*Rejected — naming each script in the workflow.* A per-file list is exactly how this gap opened: three hook matrices were written, one per gate, and none was added to CI. #270 added an explicit `chain.tests.sh` step to `dotnet.yml` and then removed it again, leaving all four equally ungated. Discovery by name (`git ls-files '*.tests.sh'`) gates the next matrix the moment it is committed, rather than the moment its author remembers the workflow.

*Rejected — one job that loops over the scripts.* A loop would either stop at the first failure or need its own accumulate-and-report logic, and either way the run's summary would name the job, not the script. A dynamic `strategy.matrix` with `fail-fast: false` makes each script its own entry, so every matrix runs to completion and each failure is already labelled with the script that produced it.

*Rejected — a step in `dotnet.yml`, after `dotnet test`.* A step there is skipped when the build or test ahead of it fails, so the one signal about the harness would go missing precisely on the runs where the harness is most suspect. A separate workflow depends on nothing the .NET job produces, runs in parallel, and reports either way.

*Accepted cost — an extra `discover` job.* A GitHub matrix cannot be computed inside the job it parametrises, so the discovery command lives in its own job and hands the list over as an output. That is two checkouts instead of one; both are seconds, against a per-file list that would need editing forever. The matrix job carries `if: needs.discover.outputs.scripts != '[]'`, because an empty matrix vector is a job failure in GitHub Actions, and a repo with no matrices at all is a legitimate state rather than a red build.

**Date:** 2026-09-25
