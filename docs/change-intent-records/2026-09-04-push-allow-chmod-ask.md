# Allowing `git push`, and moving `chmod` from `deny` to `ask`

**Intent:** Stop `/raise-pr` stalling at its push step, and stop `chmod` being a hard block with no approval path.

**Behaviour:**
- Given a `/ship` run in the project's own `defaultMode`, when it reaches `/raise-pr` step 7, then the push proceeds without a prompt.
- Given an agent that needs to set an executable bit, when it runs `chmod`, then the decision is surfaced rather than refused outright.
- Given a command naming privilege escalation (`sudo`, `su`, `chown`) or a network binary (`curl`, `wget`, `ssh`, `scp`), when it runs in any mode, then it is still refused.

**Constraints:**
- `deny` is enforced even in `bypassPermissions`, so a deny rule is a silent hard block mid-agent — no prompt, no approval path. That is right for exfiltration and privilege escalation, and wrong for a local file-mode change that grants no capability a shell does not already have.
- The matcher scans the whole command, not just its prefix. A denied binary named anywhere in a compound command — including inside a heredoc body — blocks the entire call.

**Decisions:**

*Rejected — dropping `chmod` from the rules entirely.* It would have removed the friction just as effectively, since an unmatched command runs under `bypassPermissions` and prompts in every other mode. `ask` was kept instead because `chmod` is the one entry here that can widen a file's reach to other users, which is worth a deliberate decision rather than a silent pass, and the entry documents that judgement where a deletion would leave nothing behind.

*Rejected — leaving `git push` on `ask` and relying on the operator to approve it.* That is what stalled the pipeline in the first place, at the most expensive possible point: after the suite, the reviewers, the fix commits and the PR body were all done. `ask` also cannot work unattended at all — see the caveat below.

**Caveat — `ask` is not a universal improvement on `deny`.** In an interactive session an `ask` rule prompts, which is the whole point of this change. In a headless `claude -p` worker it *denies silently*, because there is no surface to prompt on — indistinguishable in effect from the `deny` it replaced. So this restores `chmod`'s approval path for interactive work only; an unattended lane worker still loses it, and still loses it quietly. The originating issue argued that "`ask` at least surfaces the decision", which holds interactively and not headlessly. If a lane worker ever needs `chmod`, the fix is an `allow` rule, not this one. The interactive-versus-headless split is documented in [`../agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md#permissions-and-unattended-runs).

*Note on the issue's evidence.* The issue cites `scripts/plugin-report.sh` being committed `100644` as damage caused by the deny rule. That has since been corrected — the file is `100755`, matching every sibling in `scripts/` and `.claude/hooks/` — so no file-mode repair was needed here.

**Date:** 2026-09-04
