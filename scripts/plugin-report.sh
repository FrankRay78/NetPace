#!/usr/bin/env bash
#
# plugin-report.sh — read-only report on the harness tooling this repo declares.
#
# Covers four tools: read-once, context-mode and rtk (the token/context tooling named in
# docs/agentic-workflow.md) plus pr-review-toolkit, which supplies the reviewer agents /ship
# calls. Detailed descriptions and install commands for the first three live in
# docs/wsl-claude-sandbox.md, step 7.
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
# like any other, and it records a context-mode session of its own. It is run from a neutral
# directory so it does not execute this repo's hooks. Manually run: not a hook, not wired into CI or /ship, no --check
# mode and no exit-code contract. To install what it reports missing, run
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

# Verdict codes. UNKNOWN is load-bearing: it is what keeps a failed lookup out of the `no`
# column. NA marks a cell there is no probe for, so a reader does not read a duplicated value
# as corroboration.
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
# Every expansion uses the ${a[@]+"${a[@]}"} form: an EMPTY array under `set -u` is a fatal
# unbound-variable error on bash < 4.4, and macOS ships bash 3.2 *and* no timeout(1) — so the
# empty-array branch and the old-bash branch are the same box, where the plain form aborts the
# whole report mid-TOOLING.

# </dev/null is load-bearing, not tidiness: a probed tool that reads fd 0 (context-mode's
# `statusline` does) otherwise blocks on the terminal — 10s to a false timeout with timeout(1)
# present, and forever without it.
probe() { ${TIMEOUT[@]+"${TIMEOUT[@]}"} "$@" </dev/null >/dev/null 2>&1; }

# Map a probe's exit status onto a verdict. Only the tool's own failure code (1) is a `no`;
# every other non-zero is a lookup that never reached a verdict — 124 timed out, 126/127 could
# not launch, 139 crashed. Assigning a raw $? instead would be worse than wrong: a probe exiting
# 3 reads back out of yn() as `n/a`, which this report defines as "there is no probe for this
# cell" — asserting none was attempted when one ran and failed.
probe_verdict() {
  probe "$@"
  case $? in
    0) return "$YES" ;;
    1) return "$NO" ;;
    *) return "$UNKNOWN" ;;
  esac
}

# Does this repo reference the tool at all — in harness config, or in the workflow docs that
# describe it? Applied to all four rows so the column means one thing throughout: read-once is
# named only in docs (no config anywhere references it), and the column says so uniformly
# rather than meaning "in settings" on some rows and "in settings or docs" on others.
declared_in_repo() {
  { [ -f "$SETTINGS" ] && grep -q -- "$1" "$SETTINGS" 2>/dev/null; } && return "$YES"
  grep -rq -- "$1" "$ROOT/docs" 2>/dev/null && return "$YES"
  return "$NO"
}

plugin_enabled() {
  case "$SETTINGS_STATE" in
    nojq|unparseable) return "$UNKNOWN" ;;
    missing) return "$NO" ;;
  esac
  [ "$(jq -r --arg k "$1" '.enabledPlugins[$k] // false' "$SETTINGS" 2>/dev/null)" = "true" ]
}

# The first recorded installPath that exists on disk, empty when there is none. A plugin's own
# binaries are only reachable through this: `context-mode` declares a bin entry but is not
# npm-linked, so nothing it ships is on PATH. Plain success/failure, not a verdict — a caller
# that needs one goes through plugin_installed, which maps registry state onto the verdict codes.
plugin_install_path() {
  [ "$REGISTRY_STATE" = "ok" ] || return 1
  local p
  while IFS= read -r p; do
    [ -d "$p" ] && { printf '%s' "$p"; return 0; }
  done < <(jq -r --arg k "$1" '.plugins[$k][]?.installPath // empty' "$REGISTRY" 2>/dev/null)
  return 1
}

# Is any recorded installPath for this plugin present on disk? Deliberately scope-blind — a
# plugin is installed on this box or it is not; modelling user vs project scope would buy a
# state machine nobody reads. A registry that will not parse is `unknown`, not `no`: the
# .plugins[k][].installPath shape is Claude Code's private layout, and the day it changes this
# must report a broken probe rather than "nothing is installed".
plugin_installed() {
  case "$REGISTRY_STATE" in
    nojq|unparseable) return "$UNKNOWN" ;;
    missing) return "$NO" ;;
  esac
  plugin_install_path "$1" >/dev/null && return "$YES"
  return "$NO"
}

# Does a hook registered in the Claude home settings actually invoke this tool? Matched against
# the hook commands only — grepping the whole file would count a permission entry or a stray
# path string as a registered hook.
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
# `doctor` is context-mode's own health check and it runs from a shell — platform, storage
# paths, hooks and FTS5. Only its own verdict may produce a `no`: it exits 1 when it finds
# critical issues, so 1 is the health answer and every OTHER non-zero code is a lookup that
# never reached one — 124 timed out (doctor makes a network call, so a black-holed resolver
# lands here), 126/127 could not launch, 139 crashed. Those are `unknown`. Assigning a raw $?
# would be worse still: a probe exiting 3 would read back out of `yn` as `n/a`.
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

# pr-review-toolkit — supplies the named reviewer agents /ship calls.
declared_in_repo 'pr-review-toolkit'; pr_decl=$?
plugin_installed 'pr-review-toolkit@claude-plugins-official'; pr_inst=$?
plugin_enabled 'pr-review-toolkit@claude-plugins-official'; pr_en=$?
pr_reach=$NA   # agents are resolved by the harness at session start; nothing to probe here
line pr-review-toolkit "$(yn $pr_decl)" "$(yn $pr_inst)" "$(yn $pr_en)" "$(yn $pr_reach)"

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
    # Every $CLAUDE_PROJECT_DIR-rooted path in a hook or statusLine command must resolve. All
    # matches per command, not just the first — compound commands (`a.sh && b.sh`) are already
    # live in this repo's settings.
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
# `stats`, not `verify` (issue #252). The two calls deliberately use different subcommands, and
# making them agree is the wrong refactor: the TOOLING reachability probe above runs `verify`,
# read-once's full diagnostic, while counters live under `stats` (`gain` is an alias for it).
# `verify` here would fill a counter row with a checklist that exits 0, so the missing figure
# would read as a healthy report.
if [ -x "$ro_dir/read-once" ]; then perf read-once "$ro_dir/read-once" stats; else echo "read-once: n/a — not installed"; fi
# context-mode's counters are NOT simply sitting on disk. The per-PID stats-pid-*.json sidecars
# under the storage root look like the answer and are not: context-mode's own statusline source
# records them as legacy and "no longer the source of truth" — they were eventually-consistent
# and PID-scoped — and on a real box they disagree with ctx_stats by a wide margin. The
# authoritative figures live behind the ctx_stats MCP tool, so the only way to report them
# truthfully is to ask the tool, which means starting a headless model. That is disclosed in the
# header rather than smuggled in; a wrong number printed confidently is the failure this whole
# report exists to catch in others.
#
# Two argument details are load-bearing, both learned the hard way in the sibling script this
# pattern comes from:
#   * the prompt MUST be the positional immediately after -p. --disallowedTools is variadic, so
#     a positional following it is eaten word-by-word as deny rules and the model gets no prompt.
#   * stdin MUST be redirected, or the call stalls waiting on the terminal.
CM_STATS_TOOL='mcp__plugin_context-mode_context-mode__ctx_stats'

# ctx_stats prints five numbered sections, and only ONE line in them is genuinely about this
# box: section 3's "All your work" total. Everything else is scoped to wherever the probe ran.
# That was measured, not assumed — the same probe run from this repo and from the Claude home
# returns an identical "All your work" figure and a DIFFERENT section-4 dollar figure ($3.84 vs
# $2.80, each stable on repeat). Section 4 reads like a box-wide bottom line and is not one, so
# it is excluded: a number that changes with the caller's directory has no business in a report
# whose product is a truthful answer. Sections 1, 2 and 5 describe the probe's own throwaway
# session. The reply is accepted as counters ONLY when both delimiters are present, because
# `claude -p` does not guarantee a verbatim echo:
# it may paraphrase, refuse, report a usage limit, or be cut short, and every one of those exits
# 0. Unrecognised text must never occupy this row — printing it under the bare `context-mode:`
# label inside a section headed "Live counters" would launder a failed lookup into an answer,
# which is the fault this whole report exists to catch in others. It is still shown, under a
# label that says what it is.
cm_reply_is_stats() {
  case "$1" in *"── 3."*) ;; *) return 1 ;; esac
  case "$1" in *"── 4."*) ;; *) return 1 ;; esac
  return 0
}

# awk, not a sed range: a sed range re-opens on a second `── 3.` (a model that echoes then
# comments) and appends to EOF, and the `$d` needed to drop the closing heading silently eats
# the last real figure whenever section 5 is missing. This enters once and leaves once.
# "This chat:" is dropped — it is the probe's own throwaway session and would read as the
# reader's. ctx_stats indents its output inconsistently, so a leading indent is stripped if
# present rather than assumed.
cm_box_wide() {
  printf '%s\n' "$1" | awk '
    /── 3\./ && !seen3 { inside = 1; seen3 = 1; next }
    /── 4\./ && inside { inside = 0 }
    inside && !/This chat:/ && NF { sub(/^  /, ""); print }
  '
}

# claude's error output routinely carries absolute paths and account detail. PERFORMANCE is
# exempt from the diffability rule, not from "do not paste someone's home directory into a
# report they will attach to an issue".
cm_show_reply() {
  local l
  while IFS= read -r l; do printf '  %s\n' "$(scrub "$l")"; done <<<"$1"
}

cm_counters_row() {
  local out rc kept wd
  # A neutral working directory, because `claude -p` inherits the cwd's project settings and
  # would otherwise run whatever hooks the invoking repo registers — including a blocking Stop
  # hook, which forces another turn and can make the captured reply the model's answer to that
  # hook rather than ctx_stats. Verified that the tool still resolves from outside a project.
  #
  # A FIXED directory, not a fresh temporary one: context-mode counts each distinct directory as
  # a project, so a throwaway per run would permanently inflate the project count in the very
  # figure this row reports (observed going 10 -> 12 while testing).
  wd=$CLAUDE_HOME; [ -d "$wd" ] || wd=/
  # --allowedTools is a permission GRANT, not a whitelist: Read/Glob/Grep/Task stay
  # auto-approved unless denied, and a model asked for context-mode's counters with ctx_stats
  # unavailable will go looking — finding the legacy stats-pid-*.json sidecars this change
  # exists to stop trusting. Deny them, so the only counters it can reach are the real ones.
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
    kept=$(cm_box_wide "$out")
    if [ -z "${kept//[[:space:]]/}" ]; then
      echo "context-mode: unknown — ctx_stats replied, but carried no box-wide total"
    else
      # The only row here whose figures came through a model round-trip; say so, so a reader can
      # tell it apart from rtk's and read-once's at a glance.
      echo "context-mode: (via ctx_stats, headless probe)"
      printf '%s\n' "$kept" | sed 's/^/  /'
    fi
  fi
}

if [ $cm_inst -eq "$NO" ]; then
  echo "context-mode: n/a — not installed"
elif [ $cm_inst -ne "$YES" ]; then
  echo "context-mode: unknown — cannot tell whether it is installed, so the counters were not read"
# Deliberately NOT gated on `enabled`: that column reports enablement for THIS repo, and the
# probe runs from a neutral directory precisely so the invoking repo's settings do not apply.
# Verified that ctx_stats resolves outside any project, so a `no` there would suppress a probe
# that works.
elif ! command -v claude >/dev/null 2>&1; then
  echo "context-mode: unknown — the counters live behind ctx_stats, and claude is not on PATH"
else
  cm_counters_row
fi
echo "pr-review-toolkit: n/a — exposes no counters"
