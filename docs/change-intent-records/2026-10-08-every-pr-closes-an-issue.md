# Every pull request closes an issue

**Intent:** Make the issue the specification in fact rather than by convention. Work was reaching `main` through pull requests that named no issue, so the chain from acceptance criteria to merged code had a hole at its last hop — and a direct push by a repository admin could skip the pull request entirely. The goal is one gate that refuses a pull request which will not close an issue when it merges, binding everyone.

**Behaviour:**

- Given a pull request whose body carries a closing keyword naming an existing, open issue in this repository, when the `issue-link` job runs, then it passes and names the issue it linked to.
- Given a pull request whose body names an issue only as a bare `#N`, or only through the Development sidebar, or only inside a fenced block or inline-code span, when the job runs, then it fails and says no closing keyword names an open issue.
- Given a pull request whose body closes a number that does not exist, is a pull request, or is already closed, when the job runs, then it fails and reports each rejected reference with the reason it was rejected.
- Given a pull request authored by `dependabot[bot]`, when the job runs, then it passes without reading the body.
- Given GitHub cannot be reached, or a tool is missing, or the body cannot be read, when the job runs, then it fails with wording that distinguishes an outage from a body carrying no link.

**Constraints:**

- The confirmed decisions fix four things that were not open to reinterpretation: a closing keyword is what "linked" means, `dependabot[bot]` is the only exemption, there are no bypass actors, and zero approving reviews are required.
- The fixed job name `issue-link` is the required-status-check context, so it lives in its own workflow where a change to the .NET matrix or the shell-test discovery cannot rename it out from under the ruleset.
- The gate must not be satisfiable by a skipped job: to GitHub a skipped job is not a failure, and `pull_request` workflows are evaluated from the head ref, so a `paths:` filter or an `if:` would let the branch adding it be the branch it stops gating.
- A pull request body is author-controlled content, so it reaches the check through the environment and is never interpolated into shell text.

**Decisions:**

**The keyword set is GitHub's nine forms, not the issue's three.** The confirmed decision names `Closes`/`Fixes`/`Resolves`. The check accepts `close`/`closes`/`closed`, `fix`/`fixes`/`fixed` and `resolve`/`resolves`/`resolved`, in any case. *Rejected: honouring literally the three spellings named.* The property the gate exists to guarantee is that merging closes an issue, and GitHub decides which spellings do that. `Fixed #332` genuinely links and closes, so rejecting it would fail a correctly-linked pull request — the decision's parenthetical reads as naming the canonical spellings, not as narrowing the grammar below GitHub's own. The looser set cannot admit a pull request that fails the real test; the stricter one can reject one that passes it.

**Code blocks are stripped before matching.** Fenced blocks and inline-code spans are removed, so a reference inside either does not count. *Rejected: matching the raw body.* GitHub does not create a closing link from inside a code block, so stripping them is tracking GitHub rather than adding a rule. It also closes a trap specific to this repository: `CLAUDE.md`, the constitution and several command prompts quote `Closes #<N>` in backticks constantly, so a pull request whose body *documents* the convention would otherwise pass the gate it was describing, having linked nothing.

**A failure to reach GitHub is reported in different words from a body with no link.** The check reads `gh`'s stderr rather than its exit code, because "could not resolve to an issue or pull request" and an auth failure both exit non-zero and are opposite answers. *Rejected: treating any lookup failure as "that reference does not link".* The two verdicts send the author to different places, and the wrong one sends them to fix a body that is already correct. This is the same fail-closed rule, and the same answer/outage seam, as `traceability-check.sh`.

**An unterminated code fence fails rather than passing as unlinked.** A fence left open hides every line below it, so the body cannot be read. *Rejected: scanning what is left.* Telling an author to add a keyword that is sitting below their broken fence is the one verdict guaranteed to be unhelpful; "an unreadable body is not an unlinked one" is the same judgement `traceability-check.sh` makes about an issue body.

**Cross-repository references are extracted in order to be rejected by name.** `owner/repo#N` and full issue URLs are parsed, then rejected unless the slug is this repository (compared case-insensitively, as GitHub treats slugs). *Rejected: matching only bare `#N` and ignoring the rest.* Ignoring them would report "no closing keyword" about a body that plainly has one, which reads as a bug in the gate rather than a verdict on the body.

**The two ruleset changes are post-merge steps, not part of this branch.** The workflow, the check and its matrix are committed; registering `issue-link` as a required status check and deleting the repository-admin bypass actor are left to be done in the repository settings after merge. *Rejected: mutating the live ruleset from the build.* Three reasons. A ruleset is not a repository artefact, so the change cannot be committed, reviewed on the pull request, or reverted with it. Requiring a context whose workflow is not yet on `main` blocks every pull request cut before this one, since no `issue-link` run is ever reported for them. And removing the admin bypass before this branch has merged would remove the escape hatch while the gate enforcing it is still unreviewed. The repository already works this way: registering a fixed-name context in `Main CI/CD` was the post-merge step on issue #319 too.

**Date:** 2026-10-08
