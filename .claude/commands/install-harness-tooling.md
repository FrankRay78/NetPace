---
description: Install the token/context tooling this harness declares but does not verify — rtk, read-once, context-mode. Reads each upstream installer first and stops for human review before any hook lands in settings.
---

Read `CLAUDE.md` for project context before proceeding.

Installs whatever [`scripts/plugin-report.sh`](../../scripts/plugin-report.sh) reports as missing. The report is the half that only looks at the box — it changes nothing here, though it does spend a little to ask context-mode for its counters; this command is the half that changes the box.

**Nothing here runs unattended.** Two of the three tools wire a `PreToolUse` hook into `~/.claude/settings.json` on your behalf — read-once's installer does it directly, and rtk's separate `init -g` step does — and `docs/agentic-workflow.md` (*Modifying the harness itself*, rule 4) makes human review of every hook before it lands **non-negotiable**. So this command stops at each of those points and shows you the diff. It also never fetches-and-executes: `.claude/settings.json` denies `Bash(curl:*)` and `Bash(wget:*)`, those denies stay as they are, and every install command below is **printed for you to run by hand**, never executed by the agent.

## Steps

1. **Report first.** Run `bash scripts/plugin-report.sh` and read the `TOOLING` section. Install only the tools **below** showing `installed=no` (the report also covers `pr-review-toolkit`, which has no installer here); skip the rest and say which you skipped. If every tool is already installed, stop and say so — there is nothing to do.

2. **Read each installer before recommending it.** For each tool you are about to install, `WebFetch` its `install.sh` and read it. Both the rtk and read-once install lines pipe a remote script straight into a shell, so the script's current contents are the only thing that makes that safe to recommend.

   - rtk — `https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh`
   - read-once — `https://raw.githubusercontent.com/Bande-a-Bonnot/Boucle-framework/main/tools/read-once/install.sh`

   **If either fetch fails, stop** and report which one. Do not print an install command for a script you could not read — an unread remote script piped into a shell is exactly the thing the `curl`/`wget` denies exist to prevent. Summarise what each script actually does (what it writes, and where) alongside the command in step 3.

3. **Print the install commands for the missing tools.** Output them for the user to run; do not run them yourself.

   This file owns these commands — print them from here. They are not specific to any one machine or install route; the tools support Linux and macOS, WSL or not.

   **Prerequisite, context-mode only:** its MCP server runs `node ${CLAUDE_PLUGIN_ROOT}/start.mjs` and its `package.json` requires **node >= 22.5.0**. Check `node --version` first. Without node the plugin still installs, registers, and loads its hooks and skills — so it looks installed — but the MCP server cannot start, and the session reports a cached connection failure. rtk (static binary) and read-once (bash + jq) do not need node.

   **rtk** — token-saving CLI proxy. `green-gate.sh` already strips a leading `rtk` when parsing a command, so the gate is written for rtk being in play.

   ```
   curl -fsSL https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh | sh
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc && source ~/.bashrc
   rtk --version

   # Wire rtk into Claude Code — this is the step that writes a PreToolUse hook
   rtk init -g
   ```

   > **Do not use `cargo install rtk`.** An unrelated project ("Rust Type Kit") owns that crate name. The symptom is the worst kind — `rtk --version` succeeds, so the install looks fine, and only `rtk gain` fails. Use the project's own installer above.

   **read-once** — stops Claude re-reading files it already has in context.

   ```
   curl -fsSL https://raw.githubusercontent.com/Bande-a-Bonnot/Boucle-framework/main/tools/read-once/install.sh | bash
   ~/.claude/read-once/read-once verify
   ```

   **context-mode** — MCP server that sandboxes large tool output outside the context window. A Claude Code plugin. Use the HTTPS marketplace URL below in either route: the marketplace default is SSH, which fails on any box without a GitHub SSH key.

   *In-session route*, when `/plugin` is available:

   ```
   /plugin marketplace add https://github.com/mksglu/context-mode
   /plugin install context-mode@context-mode
   /reload-plugins
   /context-mode:ctx-doctor
   ```

   *CLI route* — **use this whenever `/plugin` is unavailable.** It is disabled by managed policy on some enterprise accounts, and it is not offered on every surface (the VS Code extension has no `/plugin`); the symptom is `/plugin isn't available in this environment`. The two routes write the same `~/.claude/plugins/` registry, so either works and the result is identical.

   ```
   claude plugin marketplace add https://github.com/mksglu/context-mode
   claude plugin install context-mode@context-mode
   claude plugin list
   ```

   **`claude` is often not on `PATH` even though Claude Code is running** — the editor extension ships its own copy instead of a CLI install, so `command -v claude` comes back empty while the extension works fine. Its binary lives under the extension directory and is pinned to the extension version, so derive the path rather than copying one:

   ```
   CC=$(find ~ -type f -path "*anthropic.claude-code*" -name claude -perm -u+x 2>/dev/null | head -1)
   "$CC" --version   # confirm before use; empty $CC means no bundled binary was found

   "$CC" plugin marketplace add https://github.com/mksglu/context-mode
   "$CC" plugin install context-mode@context-mode
   "$CC" plugin list
   ```

   A resolved path looks like `~/.vscode-server/extensions/anthropic.claude-code-<version>-<platform>/resources/native-binary/claude` — illustrative only; the `<version>` moves on every extension update, which is why the `find` above is the copy-paste form. For repeated use, install the standalone CLI instead of relying on the bundled binary.

   Then restart the session (or reload the editor window) so the plugin loads, and run `/context-mode:ctx-doctor` to verify. `claude plugin list` is the route-independent check if no session command is available.

4. **Stop for review before each hook lands.** Two of the three tools write hooks into `~/.claude/settings.json` themselves. At **each** of these points, stop, show `git diff`-style before/after of the settings file, and wait for the user to accept it — do not proceed to the next tool until they have.

   - **After `rtk init -g`** — writes an rtk `PreToolUse` hook.
   - **After the read-once installer** — writes `PreToolUse: Read` and `PostCompact` hooks, and drops scripts into `~/.claude/read-once/`.

   If the settings file no longer parses (`jq . ~/.claude/settings.json`), the two installers have stepped on each other's `hooks` section — say so and stop; merging it is a hand edit, not an agent one.

5. **Re-run the report.** `bash scripts/plugin-report.sh` and show the `TOOLING` section again, so the change is visible as a before/after. Report anything still `installed=no` and why.

## Out of scope

- **Recording install status anywhere in the repo.** Installing is a manual, per-box job; a doc claiming a tool is installed goes stale the moment someone clones onto a new machine. `plugin-report.sh` is the live answer, which is the whole point of it — do not write the outcome into `docs/`.
- **Un-declaring a tool.** If a tool is declared but unwanted, removing its settings entries is a separate decision and a separate change.
