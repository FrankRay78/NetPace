---
description: Install the token/context tooling this harness declares but does not verify — rtk, read-once, context-mode. Reads each upstream installer first and stops for human review before any hook lands in settings.
---

Read `CLAUDE.md` for project context before proceeding.

Installs whatever [`scripts/plugin-report.sh`](../../scripts/plugin-report.sh) reports as missing. The report is the read-only half of this pair; this command is the half that changes the box.

**Nothing here runs unattended.** Two of the three installers write a `PreToolUse` hook into `~/.claude/settings.json` on your behalf, and `docs/agentic-workflow.md` (*Modifying the harness itself*, rule 4) makes human review of every hook before it lands **non-negotiable**. So this command stops at each of those points and shows you the diff. It also never fetches-and-executes: `.claude/settings.json` denies `Bash(curl:*)` and `Bash(wget:*)`, those denies stay as they are, and every install command below is **printed for you to run by hand**, never executed by the agent.

## Steps

1. **Report first.** Run `bash scripts/plugin-report.sh` and read the `TOOLING` section. Install only the tools showing `installed=no`; skip the rest and say which you skipped. If every tool is already installed, stop and say so — there is nothing to do.

2. **Read each installer before recommending it.** For each tool you are about to install, `WebFetch` its `install.sh` and read it. Both the rtk and read-once install lines pipe a remote script straight into a shell, so the script's current contents are the only thing that makes that safe to recommend.

   - rtk — `https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh`
   - read-once — `https://raw.githubusercontent.com/Bande-a-Bonnot/Boucle-framework/main/tools/read-once/install.sh`

   **If either fetch fails, stop** and report which one. Do not print an install command for a script you could not read — an unread remote script piped into a shell is exactly the thing the `curl`/`wget` denies exist to prevent. Summarise what each script actually does (what it writes, and where) alongside the command in step 3.

3. **Print the install commands for the missing tools.** Output them for the user to run; do not run them yourself.

   **rtk** — token-saving CLI proxy. `green-gate.sh` already strips a leading `rtk` when parsing a command, so the gate is written for rtk being in play.

   ```
   curl -fsSL https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh | sh
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc && source ~/.bashrc
   rtk --version
   ```

   > **Do not use `cargo install rtk`.** An unrelated project ("Rust Type Kit") owns that crate name. The symptom is the worst kind — `rtk --version` succeeds, so the install looks fine, and only `rtk gain` fails. Use the project's own installer above.

   **read-once** — suppresses re-reads of files already in context.

   ```
   curl -fsSL https://raw.githubusercontent.com/Bande-a-Bonnot/Boucle-framework/main/tools/read-once/install.sh | bash
   ~/.claude/read-once/read-once verify
   ```

   **context-mode** — MCP server that sandboxes large tool output outside the context window. A Claude Code plugin, so it installs from inside a session, not from a shell. Use the HTTPS marketplace URL: the default is SSH, which the WSL sandbox deliberately does not have (see [`docs/wsl-claude-sandbox.md`](../../docs/wsl-claude-sandbox.md) step 6).

   ```
   /plugin marketplace add https://github.com/mksglu/context-mode
   /plugin install context-mode@context-mode
   /reload-plugins
   /context-mode:ctx-doctor
   ```

4. **Stop for review before each hook lands.** Two installers write hooks into `~/.claude/settings.json` themselves. At **each** of these points, stop, show `git diff`-style before/after of the settings file, and wait for the user to accept it — do not proceed to the next tool until they have.

   - **After `rtk init -g`** — writes an rtk `PreToolUse` hook.
   - **After the read-once installer** — writes `PreToolUse: Read` and `PostCompact` hooks, and drops scripts into `~/.claude/read-once/`.

   If the settings file no longer parses (`jq . ~/.claude/settings.json`), the two installers have stepped on each other's `hooks` section — say so and stop; merging it is a hand edit, not an agent one.

5. **Re-run the report.** `bash scripts/plugin-report.sh` and show the `TOOLING` section again, so the change is visible as a before/after. Report anything still `installed=no` and why.

## Out of scope

- **Recording install status anywhere in the repo.** Installing is a manual, per-box job; a doc claiming a tool is installed goes stale the moment someone clones onto a new machine. `plugin-report.sh` is the live answer, which is the whole point of it — do not write the outcome into `docs/`.
- **Un-declaring a tool.** If a tool is declared but unwanted, removing its settings entries is a separate decision and a separate change.
