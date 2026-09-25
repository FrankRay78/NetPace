# Every `*.tests.sh` runs in CI, discovered by name

**Intent:** Close the gap where the repo's four shell test matrices — `green-gate.tests.sh`, `no-skipped-tests.tests.sh`, `traceability-gate.tests.sh`, `chain.tests.sh` — were verified only when someone remembered to run them. None is a `dotnet test` project, so `dotnet.yml` never saw them, and Constitution Principle I asks that a tool which can run in CI be gated there rather than verified once by hand.

**Behaviour:**
- Given a pull request on which any committed `*.tests.sh` exits non-zero, when CI runs, then the job fails and the failing script is named.
- Given a `*.tests.sh` committed anywhere in the repo, when CI runs, then it is executed without anyone having edited a workflow.
- Given a pull request on which the .NET build or test job is red, when CI runs, then the shell matrices still report their own verdict.
- Given a pull request on which every matrix passes, when CI runs, then this gate does not fail it.
- Given a pull request on which no `*.tests.sh` is discovered at all, when CI runs, then the run fails and says the convention has drifted, rather than reporting a pass for having run nothing.

**Constraints:**
- The matrices need bash, jq, git, `gh` and GNU `timeout`, all preinstalled on `ubuntu-latest`, and a non-root runner. They stand up their own throwaway sandboxes and, where they commit, configure their own git identity, so the runner needs no setup step. `gh` and `timeout` are `chain.sh`'s own preflight requirements (`chain.tests.sh` stubs only `claude`), and `no-skipped-tests.tests.sh` has a `chmod 000` case that root would read straight through — so a move to a slim `container:` or self-hosted runner has to supply all five tools and an unprivileged user.
- The hooks' own `--check` modes stay out of CI. They are hook-only by design; what is gated here is their test matrices, not the gates themselves.

**Decisions:**

*Rejected — naming each script in the workflow.* A per-file list is exactly how this gap opened: three hook matrices were written, one per gate, and none was added to CI. #270 added an explicit `chain.tests.sh` step to `dotnet.yml` and then removed it again, leaving all four equally ungated. Discovery by name (`git ls-files '*.tests.sh'`) gates the next matrix the moment it is committed, rather than the moment its author remembers the workflow.

*Rejected — one job that loops over the scripts.* A loop would either stop at the first failure or need its own accumulate-and-report logic, and either way the run's summary would name the job, not the script. A dynamic `strategy.matrix` with `fail-fast: false` makes each script its own entry, so every matrix runs to completion and each failure is already labelled with the script that produced it.

*Rejected — a step in `dotnet.yml`, after `dotnet test`.* A step there is skipped when the build or test ahead of it fails, so the one signal about the harness would go missing precisely on the runs where the harness is most suspect. A separate workflow depends on nothing the .NET job produces, runs in parallel, and reports either way.

*Rejected — treating an empty discovery result as a legitimate pass.* An earlier draft skipped the matrix job when no script was found, on the grounds that a repo with no matrices is a legitimate state rather than a red build. True in the abstract, false here: there are four matrices and two documents now promise in bold that they run, so the only way to discover nothing is drift — a renamed convention, a mistyped pathspec, a checkout that no longer sees the scripts. Each of those would have reported green having verified nothing, which is the silent non-coverage Principle X exists to stop, arrived at through the workflow rather than through a `Skip`. `discover` now fails loudly on a count of zero, and the matrix job is unconditional.

*Accepted cost — an extra `discover` job.* A GitHub matrix cannot be computed inside the job it parametrises, so the discovery command lives in its own job and hands the list over as an output. That is two checkouts instead of one; both are seconds, against a per-file list that would need editing forever.

*Accepted cost — a third `gate` job whose only output is a verdict.* `name: ${{ matrix.script }}` gives each entry a name derived from a filename, so the matrix produces no stable status-check context for branch protection to require — and requiring the four current names would rebuild the rejected per-file list inside repository settings, where it is less visible than a workflow file and where a rename silently wedges the PR on a check that never reports. The fixed-name `shell-tests` job aggregates the two results instead, comparing each against `success` explicitly because GitHub counts `skipped` and `cancelled` as neither pass nor fail. **It is not yet a required check**: the `Main CI/CD` ruleset requires only `build`, so until `shell-tests` is added to it a red matrix is reported on the PR but does not block the merge.

**Date:** 2026-09-25
