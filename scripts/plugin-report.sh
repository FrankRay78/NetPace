#!/usr/bin/env bash
#
# plugin-report.sh — read-only report on the harness tooling this repo declares.
#
# Covers five tools: read-once, context-mode and rtk (the token/context tooling), pr-review-toolkit,
# which supplies the reviewer agents /gaffer:verify calls, and gaffer, which supplies the harness
# commands themselves. /install-harness-tooling installs whichever of the first three is missing.
#
# WHY. Those tools are traced through this repo by several mechanisms — `Bash(rtk …)` and
# `mcp__plugin_context-mode_context-mode__*` allow-entries in .claude/settings.json, an
# `enabledPlugins` block, and an `rtk` prefix-strip in green-gate.sh's strip_cmd_prefixes() —
# but nothing verifies that any of them is installed on the box. A tool that silently isn't
# there costs exactly what one that is there costs; you just stop getting the benefit, and
# nothing reports it because nothing looks.
#
# WHAT IT COSTS. This installs nothing and changes no file in this repo — but it is not inert,
# and does not claim to be. Reporting context-mode's counters truthfully means asking
# context-mode, and its figures live behind an MCP tool, so the PERFORMANCE section starts a
# headless `claude -p`: that spends money, takes seconds, and needs both the network and a
# logged-in CLI. The network is reached once more, by `context-mode doctor`, which compares the
# installed version against the npm registry. It writes in three places, none of them in this
# repo: context-mode's own CLI creates its empty storage directories under the Claude home when
# they are absent, the nested session leaves a transcript under the Claude home's projects tree
# like any other, and it records a context-mode session of its own. The nested session runs in
# this repo, so this repo's hooks run with it. Manually run: not a hook, not wired into CI or
# /gaffer:verify, no --check mode and no exit-code contract. To install what it reports missing, run
# /install-harness-tooling.
#
# "I COULD NOT LOOK" IS NOT "NO". Every probe that cannot reach a verdict — jq absent, a
# settings or registry file that will not parse, a probe that errors — reports `unknown`, never
# a confident `no`. A report whose whole product is a truthful yes/no must not launder a failed
# lookup into an answer; that is the exact failure this script exists to catch in others.
#
# DIFFABILITY IS THE POINT. The intended use is running this on two boxes and diffing, so
# TOOLING / CONFIG / HOOKS carry no timestamps and no raw millisecond figures, and no column
# alignment (which would reflow every line the moment a longer tool name is added). Paths are
# scrubbed to repo- or $HOME-relative form on the way out. PERFORMANCE is explicitly exempt:
# those are live counters that move every session, so cross-box diffs use the other three.
#
# Overrides: NETPACE_CLAUDE_HOME selects the Claude home to inspect (default ~/.claude),
# following the NETPACE_* convention the gates in .claude/hooks/ use. CLAUDE_PROJECT_DIR is
# Claude Code's own variable and is honoured if set (default: the repo containing this script).

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
CLAUDE_HOME="${NETPACE_CLAUDE_HOME:-${HOME:-}/.claude}"
SETTINGS="$ROOT/.claude/settings.json"
REGISTRY="$CLAUDE_HOME/plugins/installed_plugins.json"
HOME_SETTINGS="$CLAUDE_HOME/settings.json"

# UNKNOWN keeps a failed lookup out of the `no` column; NA marks a cell there is no probe for, so
# a duplicated value does not read as corroboration.
YES=0; NO=1; UNKNOWN=2; NA=3

have_jq=1
command -v jq >/dev/null 2>&1 || have_jq=0

# Classify a JSON file once: ok / missing / unparseable / nojq. Everything downstream branches
# on this rather than swallowing jq errors at the call site.
json_state() {
  [ "$have_jq" -eq 0 ] && { printf 'nojq'; return; }
  [ -f "$1" ] || { printf 'missing'; return; }
  jq -e . "$1" >/dev/null 2>&1 && printf 'ok' || printf 'unparseable'
}

SETTINGS_STATE=$(json_state "$SETTINGS")
REGISTRY_STATE=$(json_state "$REGISTRY")
HOME_SETTINGS_STATE=$(json_state "$HOME_SETTINGS")

# Strip machine-specific prefixes from anything echoed back, so two boxes produce the same text.
scrub() {
  local s="$1"
  s=${s//"$ROOT"\//}
  s=${s//"$ROOT"/.}
  [ -n "${HOME:-}" ] && s=${s//"$HOME"/\~}
  printf '%s' "$s"
}

yn() {
  case "$1" in
    0) printf 'yes' ;;
    1) printf 'no' ;;
    3) printf 'n/a' ;;
    *) printf 'unknown' ;;
  esac
}

# Never let a hung binary hang the report. Empty when timeout(1) is unavailable.
have_timeout=1
command -v timeout >/dev/null 2>&1 || have_timeout=0
TIMEOUT=(); TIMEOUT_AI=()
# The model call in PERFORMANCE needs its own, far longer budget; 10s would guarantee a timeout.
if [ "$have_timeout" -eq 1 ]; then TIMEOUT=(timeout 10); TIMEOUT_AI=(timeout 180); fi
# Every expansion uses the ${a[@]+"${a[@]}"} form: an empty array under `set -u` is fatal before
# bash 4.4, including the 3.2 macOS ships, which is also the box with no timeout(1).

# </dev/null is load-bearing: a probed tool that reads fd 0 otherwise blocks on the terminal,
# which is a false timeout with timeout(1) present and a hang without it.
probe() { ${TIMEOUT[@]+"${TIMEOUT[@]}"} "$@" </dev/null >/dev/null 2>&1; }

# Maps a probe's exit status onto a verdict. Only the tool's own 1 is a `no`; a raw $? would read
# a probe exiting 3 back out of yn() as `n/a`, claiming no probe ran when one ran and failed.
probe_verdict() {
  probe "$@"
  case $? in
    0) return "$YES" ;;
    1) return "$NO" ;;
    *) return "$UNKNOWN" ;;
  esac
}

# Does this repo reference the tool at all, in config or in docs? Applied to every row, so the
# column cannot mean "in settings" on some rows and "in settings or docs" on others.
declared_in_repo() {
  { [ -f "$SETTINGS" ] && grep -q -- "$1" "$SETTINGS" 2>/dev/null; } && return "$YES"
  grep -rq -- "$1" "$ROOT/docs" "$ROOT/.claude/commands" 2>/dev/null && return "$YES"
  return "$NO"
}

plugin_enabled() {
  case "$SETTINGS_STATE" in
    nojq|unparseable) return "$UNKNOWN" ;;
    missing) return "$NO" ;;
  esac
  [ "$(jq -r --arg k "$1" '.enabledPlugins[$k] // false' "$SETTINGS" 2>/dev/null)" = "true" ]
}

# The first recorded installPath that exists on disk, empty when there is none: a plugin's own
# binaries are reachable only through this, nothing it ships being on PATH.
plugin_install_path() {
  [ "$REGISTRY_STATE" = "ok" ] || return 1
  local p
  while IFS= read -r p; do
    [ -d "$p" ] && { printf '%s' "$p"; return 0; }
  done < <(jq -r --arg k "$1" '.plugins[$k][]?.installPath // empty' "$REGISTRY" 2>/dev/null)
  return 1
}

# Is any recorded installPath present on disk? Deliberately scope-blind. A registry that will not
# parse is `unknown`: the shape is Claude Code's private layout, and a change must read as broken.
plugin_installed() {
  case "$REGISTRY_STATE" in
    nojq|unparseable) return "$UNKNOWN" ;;
    missing) return "$NO" ;;
  esac
  plugin_install_path "$1" >/dev/null && return "$YES"
  return "$NO"
}

# Matched against the hook commands only: grepping the whole file would count a permission entry
# or a stray path string as a registered hook.
hooked_in_claude_home() {
  case "$HOME_SETTINGS_STATE" in
    nojq|unparseable) return "$UNKNOWN" ;;
    missing) return "$NO" ;;
  esac
  jq -e --arg t "$1" '[.hooks[]?[]?.hooks[]?.command // empty] | any(contains($t))' \
    "$HOME_SETTINGS" >/dev/null 2>&1
}

line() { printf '%s: declared=%s installed=%s enabled=%s reachable=%s\n' "$@"; }

# --- TOOLING ----------------------------------------------------------------------
echo "== TOOLING =="
echo "Expected tool -> declared (this repo references it) / installed (present on this box) / enabled (switched on for this repo) / reachable (probe succeeds, where the tool has one)."
echo
if [ "$have_jq" -eq 0 ]; then
  echo "WARNING: jq is not installed — installed/enabled cannot be determined and read 'unknown' below, which is NOT the same as 'no'."
  echo
fi
if [ "$SETTINGS_STATE" = "unparseable" ]; then
  echo "WARNING: $(scrub "$SETTINGS") does not parse — enabled cannot be determined."
  echo
fi
if [ "$have_timeout" -eq 0 ]; then
  echo "WARNING: timeout(1) is not installed — probes below run unbounded, including the model call in PERFORMANCE, which is the one that bills. A hung tool will hang this report."
  echo
fi

# read-once — stops Claude re-reading files it already has in context.
ro_dir="$CLAUDE_HOME/read-once"
declared_in_repo 'read-once'; ro_decl=$?
[ -d "$ro_dir" ]; ro_inst=$?
hooked_in_claude_home 'read-once'; ro_en=$?
if [ -x "$ro_dir/read-once" ]; then probe_verdict "$ro_dir/read-once" verify; ro_reach=$?; else ro_reach=$NO; fi
line read-once "$(yn $ro_decl)" "$(yn $ro_inst)" "$(yn $ro_en)" "$(yn $ro_reach)"

# context-mode — MCP server that sandboxes large tool output outside the context window.
declared_in_repo 'context-mode'; cm_decl=$?
plugin_installed 'context-mode@context-mode'; cm_inst=$?
plugin_enabled 'context-mode@context-mode'; cm_en=$?
# Only `doctor`'s own exit 1 is a `no`; every other non-zero code is a lookup that never reached
# a verdict and is `unknown`. A raw $? would read a probe exiting 3 back out of `yn` as `n/a`.
cm_path="$(plugin_install_path 'context-mode@context-mode')"
cm_cli="${cm_path:+$cm_path/cli.bundle.mjs}"
if [ $cm_inst -ne "$YES" ]; then
  cm_reach=$cm_inst
elif [ ! -f "$cm_cli" ] || ! command -v node >/dev/null 2>&1; then
  cm_reach=$UNKNOWN
else
  probe_verdict node "$cm_cli" doctor; cm_reach=$?
fi
line context-mode "$(yn $cm_decl)" "$(yn $cm_inst)" "$(yn $cm_en)" "$(yn $cm_reach)"

# rtk — token-saving CLI proxy. green-gate.sh strips a leading `rtk` when parsing a command,
# so the gate is already written on the assumption that rtk may be in play.
declared_in_repo 'rtk'; rtk_decl=$?
command -v rtk >/dev/null 2>&1; rtk_inst=$?
hooked_in_claude_home 'rtk'; rtk_en=$?
if [ $rtk_inst -eq "$YES" ]; then probe_verdict rtk --version; rtk_reach=$?; else rtk_reach=$NO; fi
line rtk "$(yn $rtk_decl)" "$(yn $rtk_inst)" "$(yn $rtk_en)" "$(yn $rtk_reach)"

# pr-review-toolkit — supplies the named reviewer agents /gaffer:verify calls.
declared_in_repo 'pr-review-toolkit'; pr_decl=$?
plugin_installed 'pr-review-toolkit@claude-plugins-official'; pr_inst=$?
plugin_enabled 'pr-review-toolkit@claude-plugins-official'; pr_en=$?
pr_reach=$NA   # agents are resolved by the harness at session start; nothing to probe here
line pr-review-toolkit "$(yn $pr_decl)" "$(yn $pr_inst)" "$(yn $pr_en)" "$(yn $pr_reach)"

# `installed` reads the user-level registry, so a box that gets gaffer only through this repo's
# settings reports declared and enabled but not installed. That is the honest answer.
declared_in_repo 'gaffer@gaffer-claude'; gf_decl=$?
plugin_installed 'gaffer@gaffer-claude'; gf_inst=$?
plugin_enabled 'gaffer@gaffer-claude'; gf_en=$?
gf_reach=$NA   # commands are resolved by the harness at session start; nothing to probe here
line gaffer "$(yn $gf_decl)" "$(yn $gf_inst)" "$(yn $gf_en)" "$(yn $gf_reach)"

# --- CONFIG -----------------------------------------------------------------------
echo
echo "== CONFIG =="
echo "Unresolvable hook/statusLine paths, and allow-entries that are dangling or duplicated."
echo
config_findings=0
config_checked=0
note() { printf '%s\n' "$1"; config_findings=$((config_findings + 1)); }

case "$SETTINGS_STATE" in
  missing) note "missing-settings: $(scrub "$SETTINGS")" ;;
  nojq) note "unchecked: jq not installed, cannot parse settings" ;;
  unparseable) note "unparseable-settings: $(scrub "$SETTINGS")" ;;
  ok)
    # All matches per command, not just the first: compound commands (`a.sh && b.sh`) are
    # already live in this repo's settings.
    while IFS= read -r cmd; do
      [ -n "$cmd" ] || continue
      found=0
      while IFS= read -r m; do
        [ -n "$m" ] || continue
        p=${m#\$}; p=${p#\{}; p=${p#CLAUDE_PROJECT_DIR}; p=${p#\}}
        p=${p%%[;\&|\)]*}                       # drop trailing shell punctuation
        # A bare `$CLAUDE_PROJECT_DIR` (as in `cd "$CLAUDE_PROJECT_DIR" && …`) names the repo
        # root, not a script — checking it would report the repo itself as an unresolved path.
        [ -n "$p" ] && [ "$p" != "/" ] || continue
        found=1
        config_checked=$((config_checked + 1))
        [ -f "$ROOT$p" ] || note "unresolved-path: $(scrub "$ROOT$p")"
      done < <(printf '%s\n' "$cmd" | grep -oE '\$\{?CLAUDE_PROJECT_DIR\}?[^"'"'"'[:space:]]*')
      # A command naming a script by some other route cannot be checked from here — say so,
      # rather than letting the ok: line below imply it was covered.
      if [ "$found" -eq 0 ] && [[ "$cmd" =~ \.(sh|ps1|mjs|js|py)([[:space:]]|\"|$) ]]; then
        note "unchecked-path: script referenced without \$CLAUDE_PROJECT_DIR — $(scrub "$cmd")"
      fi
    done < <(jq -r '[(.hooks // {} | to_entries[].value[]?.hooks[]?.command // empty), (.statusLine.command // empty)][]' "$SETTINGS" 2>/dev/null)

    # An MCP allow-entry for a plugin that is not installed is inert config.
    mcp_count=$(jq -r '[.permissions.allow[]? | select(startswith("mcp__plugin_context-mode"))] | length' "$SETTINGS" 2>/dev/null)
    if [ "${mcp_count:-0}" -gt 0 ] && [ $cm_inst -eq "$NO" ]; then
      note "dangling-mcp-allow: mcp__plugin_context-mode_context-mode__* ($mcp_count entries; context-mode not installed)"
    fi

    # Duplicate allow-entries are harmless but always unintentional.
    while IFS= read -r dup; do
      note "duplicate-allow-entry: $dup"
    done < <(jq -r '.permissions.allow // [] | group_by(.)[] | select(length > 1) | "\(.[0]) (x\(length))"' "$SETTINGS" 2>/dev/null)
    ;;
esac
[ "$config_findings" -eq 0 ] && echo "ok: $config_checked hook/statusLine path(s) resolve, no dangling or duplicate allow-entries"

# --- HOOKS ------------------------------------------------------------------------
echo
echo "== HOOKS =="
echo "Registered hooks, and what each costs per invocation."
echo
if [ "$SETTINGS_STATE" = "ok" ]; then
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
    shown=${cmd//\"/}
    shown=${shown//\$\{CLAUDE_PROJECT_DIR\}\//}
    shown=${shown//\$CLAUDE_PROJECT_DIR\//}
    printf '%s/%s if=%s: %s cost=%s\n' "$event" "$matcher" "$cond" "$(scrub "$shown")" "$cost"
  done
else
  echo "cannot list hooks: settings are $SETTINGS_STATE"
fi

# --- PERFORMANCE ------------------------------------------------------------------
echo
echo "== PERFORMANCE =="
echo "Live counters. EXEMPT from the diffability rule above — these move every session, so diff the other three sections across boxes, not this one."
echo
# A probe that errors must not be laundered into something that looks like counter output.
perf() {
  local label="$1"; shift
  local out rc
  out=$(${TIMEOUT[@]+"${TIMEOUT[@]}"} "$@" </dev/null 2>&1); rc=$?
  if [ $rc -ne 0 ]; then printf '%s: PROBE FAILED (exit %s)\n' "$label" "$rc"; else printf '%s:\n' "$label"; fi
  printf '%s\n' "$out" | sed 's/^/  /'
}
if [ $rtk_inst -eq "$YES" ]; then perf rtk rtk gain; else echo "rtk: n/a — not installed"; fi
# `stats`, not the `verify` the reachability probe above runs: making the two agree is the wrong
# refactor, because `verify` would fill a counter row with a checklist that exits 0.
if [ -x "$ro_dir/read-once" ]; then perf read-once "$ro_dir/read-once" stats; else echo "read-once: n/a — not installed"; fi
# The stats-pid-*.json sidecars look like the answer and are not: the authoritative figures are
# behind the MCP tool, so reporting them truthfully means starting a headless model.
#
# The prompt MUST be the positional immediately after -p, --disallowedTools being variadic, and
# stdin MUST be redirected or the call stalls waiting on the terminal.
CM_STATS_TOOL='mcp__plugin_context-mode_context-mode__ctx_stats'

# Printed whole rather than sliced, an earlier revision having thrown the headline totals away.
# The reply is scoped to the directory the probe runs in, so it runs from this repo.
#
# `claude -p` may paraphrase, refuse or be cut short, and every one of those exits 0, so
# unrecognised text must never occupy this row under a counters label.
#
# The END anchor is the load-bearing half: a blocking Stop hook forces another turn whose answer
# is appended after genuine counters, which both markers still match.
cm_reply_is_stats() {
  case "$1" in *"── 3."*) ;; *) return 1 ;; esac
  case "$1" in *"── 4."*) ;; *) return 1 ;; esac
  local last
  last=$(printf '%s\n' "$1" | grep -v '^[[:space:]]*$' | tail -1)
  [[ "$last" =~ ^[[:space:]]*v[0-9]+\.[0-9]+\.[0-9]+[[:space:]]*$ ]]
}

# claude's error output carries absolute paths and account detail, and PERFORMANCE is exempt from
# the diffability rule, not from keeping a home directory out of an attached report.
cm_show_reply() {
  local l
  while IFS= read -r l; do printf '  %s\n' "$(scrub "$l")"; done <<<"$1"
}

cm_counters_row() {
  local out rc wd
  wd=$ROOT; [ -d "$wd" ] || wd=/
  # --allowedTools is a permission GRANT, not a whitelist, so a model whose ctx_stats is
  # unavailable goes looking and finds the legacy sidecars. Deny them.
  out=$(cd "$wd" 2>/dev/null && ${TIMEOUT_AI[@]+"${TIMEOUT_AI[@]}"} claude -p \
    "Call the $CM_STATS_TOOL tool once and output its result verbatim. Add no commentary of your own." \
    --allowedTools "$CM_STATS_TOOL" \
    --disallowedTools Bash Edit Write WebFetch WebSearch Read Glob Grep Task NotebookEdit \
    </dev/null 2>&1); rc=$?
  if [ $rc -ne 0 ]; then
    printf 'context-mode: PROBE FAILED (exit %s)\n' "$rc"
    cm_show_reply "$out"
  elif [ -z "${out//[[:space:]]/}" ]; then
    echo "context-mode: unknown — the counters probe exited 0 but returned nothing"
  elif ! cm_reply_is_stats "$out"; then
    echo "context-mode: unknown — the probe reply is not ctx_stats output, so nothing below is a counter reading"
    echo "  raw probe reply (NOT counters):"
    cm_show_reply "$out"
  else
    # Labelled because this is the only row whose figures came through a model round-trip, so a
    # reader takes "this chat" in the reply as the probe's own session.
    echo "context-mode: (via ctx_stats, headless probe)"
    cm_show_reply "$out"
  fi
}

if [ $cm_inst -eq "$NO" ]; then
  echo "context-mode: n/a — not installed"
elif [ $cm_inst -ne "$YES" ]; then
  echo "context-mode: unknown — cannot tell whether it is installed, so the counters were not read"
# Deliberately NOT gated on `enabled`, which reports this repo while ctx_stats resolves through
# the user-level install: a `no` there would suppress a probe that works.
elif ! command -v claude >/dev/null 2>&1; then
  echo "context-mode: unknown — the counters live behind ctx_stats, and claude is not on PATH"
else
  cm_counters_row
fi
echo "pr-review-toolkit: n/a — exposes no counters"
echo "gaffer: n/a — exposes no counters"
