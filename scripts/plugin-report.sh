#!/usr/bin/env bash
#
# plugin-report.sh — read-only report on the token/context tooling this harness declares.
#
# WHY. NetPace's config traces four tools right through the harness — settings allow-entries,
# `enabledPlugins`, an `rtk` prefix-strip in green-gate.sh, and a section in
# docs/agentic-workflow.md — but nothing verifies that any of them is actually installed on the
# box. A tool that silently isn't there costs exactly what one that is there costs; you just
# stop getting the benefit, and nothing reports it because nothing looks.
#
# READ-ONLY. This script installs nothing, edits nothing, and starts nothing. It only reads
# files and runs version/status probes on tools that are already present. It is a manually-run
# report: not a hook, not wired into CI or /ship, and there is no --check mode and no exit-code
# contract. To install what it reports missing, run /install-harness-tooling.
#
# DIFFABILITY IS THE POINT. The intended use is running this on two boxes and diffing the
# output, so TOOLING / CONFIG / HOOKS carry no timestamps, no absolute paths and no raw
# millisecond figures — and no column alignment, which would reflow every line the moment a
# longer tool name is added. PERFORMANCE is explicitly exempt: those are live counters that
# move every session, so cross-box diffs use the other three sections.
#
# Overrides (NETPACE_* convention, matching the gates in .claude/hooks/):
#   NETPACE_CLAUDE_HOME  — Claude home to inspect (default: ~/.claude)
#   CLAUDE_PROJECT_DIR   — repo root (default: the repo containing this script)

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
CLAUDE_HOME="${NETPACE_CLAUDE_HOME:-$HOME/.claude}"
SETTINGS="$ROOT/.claude/settings.json"

have_jq=1
command -v jq >/dev/null 2>&1 || have_jq=0

# Render a path relative to the repo root, else $HOME-relative as ~/…, so output never
# carries an absolute path that differs between boxes.
rel() {
  local p="$1"
  case "$p" in
    "$ROOT"/*) printf '%s' "${p#"$ROOT"/}" ;;
    "$HOME"/*) printf '~/%s' "${p#"$HOME"/}" ;;
    *) printf '%s' "$p" ;;
  esac
}

yn() { [ "$1" -eq 0 ] && printf 'yes' || printf 'no'; }

# Does the repo's settings.json mention this string anywhere?
declared_in_settings() { [ -f "$SETTINGS" ] && grep -q -- "$1" "$SETTINGS"; }

# Is a plugin marked true under enabledPlugins in the repo's settings?
plugin_enabled() {
  [ "$have_jq" -eq 1 ] && [ -f "$SETTINGS" ] || return 1
  [ "$(jq -r --arg k "$1" '.enabledPlugins[$k] // false' "$SETTINGS" 2>/dev/null)" = "true" ]
}

# Is any recorded installPath for this plugin actually present on disk? Deliberately
# scope-blind — a plugin is installed on this box or it is not; modelling user vs project
# scope would buy a state machine nobody reads.
plugin_installed() {
  local reg="$CLAUDE_HOME/plugins/installed_plugins.json" p
  [ "$have_jq" -eq 1 ] && [ -f "$reg" ] || return 1
  while IFS= read -r p; do
    [ -n "$p" ] && [ -d "$p" ] && return 0
  done < <(jq -r --arg k "$1" '.plugins[$k][]?.installPath // empty' "$reg" 2>/dev/null)
  return 1
}

# Does any hook registered in the Claude home settings invoke this tool?
hooked_in_claude_home() {
  local s="$CLAUDE_HOME/settings.json"
  [ -f "$s" ] || return 1
  grep -q -- "$1" "$s"
}

line() { printf '%s: declared=%s installed=%s enabled=%s reachable=%s\n' "$@"; }

# --- TOOLING ----------------------------------------------------------------------
echo "== TOOLING =="
echo "Expected tool -> declared (this repo references it) / installed (present on this box) / enabled (switched on here) / reachable (probes clean)."
echo

# read-once — PreToolUse hook that suppresses re-reads of unchanged files.
ro_dir="$CLAUDE_HOME/read-once"
declared_in_settings 'read-once' || grep -rq 'read-once' "$ROOT/docs" 2>/dev/null; ro_decl=$?
[ -d "$ro_dir" ]; ro_inst=$?
hooked_in_claude_home 'read-once'; ro_en=$?
[ -x "$ro_dir/read-once" ]; ro_reach=$?
line read-once "$(yn $ro_decl)" "$(yn $ro_inst)" "$(yn $ro_en)" "$(yn $ro_reach)"

# context-mode — MCP server that sandboxes large tool output outside the context window.
declared_in_settings 'context-mode'; cm_decl=$?
plugin_installed 'context-mode@context-mode'; cm_inst=$?
plugin_enabled 'context-mode@context-mode'; cm_en=$?
cm_reach=$cm_inst
line context-mode "$(yn $cm_decl)" "$(yn $cm_inst)" "$(yn $cm_en)" "$(yn $cm_reach)"

# rtk — token-saving CLI proxy. green-gate.sh strips a leading `rtk` when parsing a command,
# so the gate is already written on the assumption that rtk may be in play.
declared_in_settings 'Bash(rtk'; rtk_decl=$?
command -v rtk >/dev/null 2>&1; rtk_inst=$?
hooked_in_claude_home '"rtk'; rtk_en=$?
if [ $rtk_inst -eq 0 ]; then rtk --version >/dev/null 2>&1; rtk_reach=$?; else rtk_reach=1; fi
line rtk "$(yn $rtk_decl)" "$(yn $rtk_inst)" "$(yn $rtk_en)" "$(yn $rtk_reach)"

# pr-review-toolkit — supplies the named reviewer agents /ship calls.
declared_in_settings 'pr-review-toolkit'; pr_decl=$?
plugin_installed 'pr-review-toolkit@claude-plugins-official'; pr_inst=$?
plugin_enabled 'pr-review-toolkit@claude-plugins-official'; pr_en=$?
pr_reach=$pr_inst
line pr-review-toolkit "$(yn $pr_decl)" "$(yn $pr_inst)" "$(yn $pr_en)" "$(yn $pr_reach)"

# --- CONFIG -----------------------------------------------------------------------
echo
echo "== CONFIG =="
echo "Settings and hook paths that do not resolve on this box."
echo
config_findings=0
note() { printf '%s\n' "$1"; config_findings=$((config_findings + 1)); }

if [ ! -f "$SETTINGS" ]; then
  note "missing-settings: $(rel "$SETTINGS")"
elif [ "$have_jq" -eq 0 ]; then
  note "skipped: jq not installed, cannot parse settings"
else
  # Every hook/statusLine command that names a script path must resolve to a real file.
  while IFS= read -r cmd; do
    [ -n "$cmd" ] || continue
    path=$(printf '%s' "$cmd" | grep -oE '\$CLAUDE_PROJECT_DIR[^"[:space:]]*' | head -1)
    [ -n "$path" ] || continue
    resolved="${path/\$CLAUDE_PROJECT_DIR/$ROOT}"
    [ -f "$resolved" ] || note "unresolved-path: $(rel "$resolved")"
  done < <(jq -r '[(.hooks // {} | to_entries[].value[]?.hooks[]?.command // empty), (.statusLine.command // empty)][]' "$SETTINGS" 2>/dev/null)

  # An MCP allow-entry for a plugin that is not installed is inert config.
  mcp_count=$(jq -r '[.permissions.allow[]? | select(startswith("mcp__plugin_context-mode"))] | length' "$SETTINGS" 2>/dev/null)
  if [ "${mcp_count:-0}" -gt 0 ] && [ $cm_inst -ne 0 ]; then
    note "dangling-mcp-allow: mcp__plugin_context-mode__* ($mcp_count entries; context-mode not installed)"
  fi

  # Duplicate allow-entries are harmless but always unintentional.
  while IFS= read -r dup; do
    [ -n "$dup" ] && note "duplicate-allow-entry: $dup"
  done < <(jq -r '.permissions.allow // [] | group_by(.)[] | select(length > 1) | "\(.[0]) (x\(length))"' "$SETTINGS" 2>/dev/null)
fi
[ "$config_findings" -eq 0 ] && echo "ok: every hook and statusLine path resolves, no dangling or duplicate allow-entries"

# --- HOOKS ------------------------------------------------------------------------
echo
echo "== HOOKS =="
echo "Registered hooks, and what each costs per invocation."
echo
if [ "$have_jq" -eq 1 ] && [ -f "$SETTINGS" ]; then
  jq -r '.hooks // {} | to_entries[] as $e | $e.value[]? as $g | $g.hooks[]? |
         [$e.key, ($g.matcher // "-"), (.if // "-"), (.command // "")] | @tsv' "$SETTINGS" 2>/dev/null |
  while IFS=$'\t' read -r event matcher cond cmd; do
    # Cost is a band, not a measurement: a millisecond figure would differ on every box and
    # break the cross-box diff this report exists to support.
    case "$cmd" in
      *"dotnet build"*|*"dotnet test"*) cost="full-suite (build + test)" ;;
      *.sh*) cost="script" ;;
      *) cost="other" ;;
    esac
    shown=$(printf '%s' "$cmd" | tr -d '"' | sed "s|\$CLAUDE_PROJECT_DIR/||")
    printf '%s/%s if=%s: %s cost=%s\n' "$event" "$matcher" "$cond" "$shown" "$cost"
  done
else
  echo "skipped: jq not installed, or settings missing"
fi

# --- PERFORMANCE ------------------------------------------------------------------
echo
echo "== PERFORMANCE =="
echo "Live counters. EXEMPT from the diffability rule above — these move every session, so diff the other three sections across boxes, not this one."
echo
if [ $rtk_reach -eq 0 ]; then
  echo "rtk:"
  rtk gain 2>&1 | sed 's/^/  /'
else
  echo "rtk: n/a — not installed"
fi
if [ $ro_reach -eq 0 ]; then
  echo "read-once:"
  "$ro_dir/read-once" verify 2>&1 | sed 's/^/  /'
else
  echo "read-once: n/a — not installed"
fi
if [ $cm_inst -eq 0 ]; then
  echo "context-mode: installed — counters are MCP-only; run ctx_stats in-session"
else
  echo "context-mode: n/a — not installed"
fi
echo "pr-review-toolkit: n/a — exposes no counters"
