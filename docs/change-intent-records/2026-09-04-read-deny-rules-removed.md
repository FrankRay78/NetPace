# Removing the `Read(…)` rules from `permissions.deny`

**Intent:** Stop a recursive read of the repository escalating to a manual approval that no permission mode grants, which interrupted `/ship` step 2 when a reviewer subagent ran `grep -rn … .`.

**Behaviour:**
- Given a reviewer subagent in an interactive session, when it greps the repository root recursively, then it completes without an approval prompt.
- Given any session, when Claude reads a file, then no `Read(…)` rule refuses it — that cover is gone, deliberately. See *What this gives up*.

**Constraints:**
- The escalation is **glob-scope-based, not existence-based**. The repository contains no `.env`, `secrets.*`, `.ssh/` or `appsettings*.json` file, yet the prompt fired naming `Read(**/.env)`, because the grep's read scope *could* contain a path the glob matches.
- There was therefore no partial fix. Every rule was `**/`-anchored and so matchable somewhere under the working directory; trimming the list would have left the escalation intact.
- It is an approval **no mode auto-grants** — `bypassPermissions` does not clear it, and neither does an `allow` rule. `Bash(grep:*)` was already allowed and the command escalated regardless.
- It is **interactive-only**. Under `claude -p` the same greps run clean, verified across three shapes including the exact compound form that prompted.
- Approval is memoised per session, so the cost was roughly one prompt per interactive session rather than one per grep.

**Decisions:**

*Rejected — converting the six rules from `deny` to `ask`.* Tested and reverted. It would have kept a surface on direct secret reads, but there was no evidence it stops the scope escalation, while it certainly weakens direct reads from never-readable to readable-after-one-approval. Weakening something real to buy something unverified is the worst of the options.

*Rejected — keeping a subset.* Ruled out by the glob-scope constraint above.

**What this gives up.** Claude can now read a `.env`, `secrets.*` or `appsettings*.json` file if one ever appears in this repository. The exposure is prospective, not live: a scan of the working tree at the time of this change found no file of any of those names, no key or certificate files, and no content matches for AWS keys, GitHub or Slack tokens, `sk-` keys, private-key headers, or quoted password/secret/token assignments. NetPace is a public CLI that holds no credentials by design, and the machine is a disposable sandbox. If the project ever does acquire a secret file, this decision needs revisiting — a `PreToolUse` hook that blocks reads of specific paths would restore the cover without reintroducing the scope escalation, because the escalation keys on `Read(…)` rules in settings rather than on hooks.

**Verification gap — read this before trusting the change.** It could not be verified in the session that made it. Establishing a RED requires an un-memoised session, and the approval had already been granted here, so a clean before/after was no longer available; the headless oracle is blind to this class by the constraint above. The reasoning is sound and the mechanism is understood, but the fix is **unconfirmed by observation**. Confirm it in a fresh interactive session: run `grep -rn "<any term>" .` from the repository root as the first such command of the session and check that no approval prompt appears.

**Date:** 2026-09-04
