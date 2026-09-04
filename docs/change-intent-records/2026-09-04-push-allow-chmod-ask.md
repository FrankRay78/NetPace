# Allowing `git push`, and moving `chmod` from `deny` to `ask`

**Intent:** Stop `/raise-pr` stalling at its push step, and stop `chmod` being a hard block with no approval path.

**Behaviour:**
- Given a `/ship` run in the project's own `defaultMode`, when it reaches `/raise-pr`'s push step, then the push proceeds without a prompt.
- Given an agent in an interactive session that needs to set an executable bit, when it runs `chmod`, then the decision is surfaced rather than refused outright. In a headless worker it is still refused — see the caveat below.
- Given a command naming privilege escalation (`sudo`, `su`, `chown`) or a network binary (`curl`, `wget`, `ssh`, `scp`), when it runs in any mode, then it is still refused.

**Constraints:**
- `deny` is enforced even in `bypassPermissions`, so a deny rule is a silent hard block mid-agent — no prompt, no approval path. That is right for exfiltration and privilege escalation, and wrong for a local file-mode change that grants no capability a shell does not already have.
- The matcher scans the whole command, not just its prefix. A denied binary named anywhere in a compound command — including inside a heredoc body — blocks the entire call. Evidenced twice: issue #250 records a blocked command beginning `SB=/tmp/…` with `chmod` eight lines later inside a heredoc, and the commit implementing this change was itself refused for naming a denied binary in its message.

**Decisions:**

*Rejected — dropping `chmod` from the rules entirely.* It would have removed the friction just as effectively, since an unmatched command runs under `bypassPermissions` and prompts in every other mode. `ask` was kept because `chmod` is the one entry here that can widen a file's reach to other users, which is worth a deliberate decision rather than a silent pass.

*Rejected — pairing the `git push` allow with a `deny` on `--force`.* [`2026-09-04-rm-off-the-ask-list.md`](2026-09-04-rm-off-the-ask-list.md) anticipated shipping both halves together. Only the `allow` shipped, deliberately: permission rules match on textual prefix, so `Bash(git push --force:*)` would catch `git push --force` and miss `git push -f`, `git push origin main --force`, and every compound form. That is the same objection which sank a narrowed `rm` rule in that CIR — a guard a flag reorder defeats is worse than none, because it reads as protection. Force-push is therefore **unguarded as of this change**, which is a real reduction in cover, not an oversight: the honest fix is a `PreToolUse` hook that parses the command rather than a prefix rule, and that is worth its own issue.

**Supersedes:** this record replaces the `git push` disposition in [`2026-09-04-rm-off-the-ask-list.md`](2026-09-04-rm-off-the-ask-list.md), which says `Bash(git push:*)` stays on `ask`. It no longer does. Both CIRs carry the same date, so that line is only readable in light of this one.

**Caveat — `ask` is not a universal improvement on `deny`.** In an interactive session an `ask` rule prompts, which is the whole point of this change. In a headless `claude -p` worker it *denies silently*, because there is no surface to prompt on — indistinguishable in effect from the `deny` it replaced. So this restores `chmod`'s approval path for interactive work only; an unattended lane worker still loses it, and still loses it quietly. The originating issue argued that "`ask` at least surfaces the decision", which holds interactively and not headlessly. If a lane worker ever needs `chmod`, the fix is an `allow` rule, not this one. The interactive-versus-headless split is documented in [`../agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md#permissions-and-unattended-runs).

*Note on the issue's evidence.* The issue cites `scripts/plugin-report.sh` being committed `100644` as damage caused by the deny rule. That has since been corrected — the file is `100755`, matching every other shell script in `scripts/` and `.claude/hooks/` (`git-red-phase-commit.ps1` and `hooks/README.md` are non-executable by design) — so no file-mode repair was needed here.

**Date:** 2026-09-04
