# Removing the `Read(…)` rules from `permissions.deny`

**Intent:** Stop a recursive read of the repository escalating to a manual approval that no permission mode grants, which interrupted `/verify`'s review whenever a reviewer subagent ran `grep -rn … .`.

**Behaviour:** `.claude/settings.json`; `docs/agentic-workflow-NetPace.md` → *Permissions*.

**Decisions:**

- *What this gives up, and when to revisit.* Claude can now read a `.env`, `secrets.*` or `appsettings*.json` file if one ever appears in this repository. The exposure was prospective, not live: a scan of the working tree at the time found no file of any of those names, no key or certificate files, and no content matches for AWS keys, GitHub or Slack tokens, `sk-` keys, private-key headers or quoted password assignments. NetPace is a public CLI that holds no credentials by design, and the machine is a disposable sandbox. **If the project ever does acquire a secret file, revisit this** — and the fix is a `PreToolUse` hook that blocks reads of specific paths, not these rules back. A hook restores the cover without reintroducing the scope escalation, because the escalation keys on `Read(…)` rules in settings rather than on hooks.
- *Rejected — keeping a subset, or converting the six rules to `ask`.* The escalation is glob-scope-based rather than existence-based, and every rule was `**/`-anchored and so matchable somewhere under the working directory, so a subset would have left it intact. `ask` was tried and reverted: no evidence it stops the escalation, and it certainly weakens direct reads from never-readable to readable-after-one-approval.

**Date:** 2026-09-04
