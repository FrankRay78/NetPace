# Refuse an executable-bit `chmod`, keep the `ask` rule behind it

**Supersedes:** [2026-09-04-push-allow-chmod-ask.md](2026-09-04-push-allow-chmod-ask.md) — its closing recommendation that "if a lane worker ever needs `chmod`, the fix is an `allow` rule". The fix taken here is a third option that record did not consider: redirect the agent to a call that needs no permission at all. Everything else in that record still stands, including its observation that the harness matcher scans a whole compound command and reads a heredoc body as commands.

**Intent:** Stop an unattended `/build` → `/verify` → `/raise-pr` chain losing time or capability to a permission decision no human is present to make. An agent that wants to run a throwaway script to verify its own finding reaches for `chmod +x script.sh`; `bash script.sh` needs no permission and does the same job, so the goal is to make the agent take the second route without anyone having to remember the rule.

**Behaviour:** Given an agent attempts to set the executable bit on a file, when the attempt is made, then it is refused with a message naming `bash script.sh` as the alternative. Given a `chmod` that cannot set the executable bit (`644`, `-x`, `u+w`, a `--reference=` copy), when the attempt is made, then the hook raises no objection and the call is governed by the existing `ask` rule as before.

**Constraints:**

- `Bash(chmod:*)` sits on `permissions.ask`. Per the generic *Permissions and unattended runs*, an `ask`-matched call prompts interactively but is **silently denied** headlessly — so in a lane worker the agent loses the capability and carries on degraded, with nothing in the run to say so. That silence, not the prompt, is the expensive half.
- The cost is measured, not hypothesised. In the #265 A/B comparisons, run 1 saw `pr-test-analyzer` block ~84 minutes on `chmod +x "$S/bin/claude"` and `/code-review`'s fork block ~86 minutes on `chmod +x t.sh`; neither review would have completed unattended. Run 2 gave both arms one line of guidance — run throwaway scripts as `bash script.sh` — and no agent reached for the gated call at all.
- `.claude/hooks/no-skipped-tests.sh` plus Constitution §X is the repo's established shape for a prohibited construct that is gate-enforced rather than left to memory. This is the same shape at smaller scale.
- The harness is edited with itself, so a false refusal can lock out the tools that would fix it. The gate therefore fails open, like its sibling `green-gate.sh`.

**Decisions:**

*Refuse in a hook rather than tighten the permission list.* A `deny` list entry would stop the call but say nothing useful; the agent would read a bare refusal and have to infer the alternative. The whole finding from run 2 is that **one line naming the alternative** was sufficient — so the mechanism has to be one that carries a message. A `PreToolUse` deny with a `permissionDecisionReason` does; a permission-list entry does not.

*Keep `Bash(chmod:*)` on `permissions.ask`.* Rejected — removing it as redundant now that the hook gives an actionable message. The hook is the actionable layer, not a complete one: it fails open on an undecidable mode, and it cannot act at all in a run where hooks are not loaded. With the `ask` rule behind it, a miss costs a prompt; without it, a miss is a free pass. The redundancy is the point.

The `ask` rule does over-ask in one visible way, accepted rather than fixed: the harness's own matcher reads a `chmod` line inside a heredoc body or a multi-line commit message as an invocation, so writing *about* this rule can still prompt. Put the message in a file and use `git commit -F <file>`.

*Narrow the refusal to executable-bit forms.* Rejected — refusing every `chmod`. `chmod 644` and `chmod -x` cannot produce the stall this record exists to prevent, and refusing them would buy nothing while making the gate feel arbitrary. The matcher reads the mode operand: an odd digit in any of the last three numeric positions, or a `+`/`=` symbolic clause whose letters include `x` or `X`.

*Match `chmod` as the head of a command segment, not as a substring.* Rejected — a whole-command substring match, as `no-skipped-tests.sh` deliberately uses. That gate can accept its over-block because it only fires when §X is already violated and fails closed by design; this one fires on every Bash call and fails open, so a false refusal is pure friction with no safety dividend. Segments are separated by `&&`, `||`, `;`, `|`, `&` and a newline — a newline chains commands exactly as the operators do, so a `chmod` line in a multi-line script is caught too.

Quoted spans and heredoc bodies are masked before splitting. This is not defensive padding: the gate refused its **own first live call**, because a `&&` inside a quoted JSON payload manufactured a segment that looked like a real invocation. Both masks earn their place in the matcher, and both are pinned by regression cases in `no-chmod.tests.sh`.

*Record the prohibition as a `CLAUDE.md` paired rule only.* Rejected — a new Constitution principle, and a `.claude/memory/` entry. The rule is narrower than Principle X and is gate-enforced, so the constitution would carry weight it does not need; and a memory entry duplicating a `CLAUDE.md` line already loaded every session is a second place to keep in sync for no extra reach.

*Register the hook without an `if` clause.* An `if: "Bash(chmod:*)"` only sees a command whose first word is `chmod`, so it would miss `cd /repo && chmod +x t.sh` — the form the segment scan exists for. `green-gate.sh` is wired the same way for the same reason. It is invoked via `bash …` so it needs no working-tree executable bit: the gate should not depend on the very thing it refuses to grant.

**Date:** 2026-09-27
