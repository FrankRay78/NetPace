#!/usr/bin/env bash
#
# green-gate.sh — `dotnet test --no-build` staleness guard.
#
# A single PreToolUse(Bash) gate: deny `dotnet test --no-build` when no test assembly has
# been built yet, or when a source is newer than the built assembly — either way a
# --no-build run would execute a stale/absent DLL and report misleading results. Promotes
# feedback_dotnet_test_no_build.
#
# SCOPE. This gate does exactly one thing: deny a `dotnet test --no-build` that would run a
# stale or absent test assembly. The "tests are green before a PR" guarantee lives elsewhere —
# a real whole-suite run inside the human-invoked `/ship` command, not in this hook.
#
# DESIGN RULE: fail OPEN. Any missing tool, unparseable input, or internal error exits
# 0 (no objection). The one action taken (the --no-build deny) is the narrow, high-confidence case
# only; every uncertain path defaults to allow, because a guard that falsely blocks is
# worse than no guard — and, for a harness we edit with itself, a false block can lock out
# the very tools that would fix it.
#
# Escape hatch (harness-safety: override-first, then tighten): set NETPACE_SKIP_GREEN_GATE=1
# to make the whole script a no-op. It announces itself on stderr so it can never be
# silently in effect.

set -uo pipefail

# --- override-first escape hatch — announced, never silent ------------------------
if [ "${NETPACE_SKIP_GREEN_GATE:-}" = "1" ]; then
  echo "green-gate: WARNING — gate BYPASSED via NETPACE_SKIP_GREEN_GATE=1 (--no-build staleness deny NOT enforced)." >&2
  exit 0
fi

# --- fail-open preconditions ------------------------------------------------------
command -v jq >/dev/null 2>&1 || exit 0
INPUT=$(cat 2>/dev/null) || exit 0
[ -n "$INPUT" ] || exit 0

jget() { printf '%s' "$INPUT" | jq -r "$1" 2>/dev/null; }

EVENT=$(jget '.hook_event_name // empty')
TOOL=$(jget '.tool_name // empty')

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# Print the first source file newer than the reference file $1, or nothing. The source root
# whose edits invalidate a build is the .NET tree under src/ (*.cs) — where NetPace keeps both
# production and *.Tests projects; obj/ and bin/ (generated .cs) are excluded so an unrelated
# restore/build can't read as "our code changed". Only roots that exist are scanned; find needs
# the roots BEFORE the expression, and -quit stops at the first hit (cheap).
first_newer_than() {
  local ref="$1" d roots=()
  for d in src; do [ -d "$ROOT/$d" ] && roots+=("$ROOT/$d"); done
  [ ${#roots[@]} -eq 0 ] && return 0
  find "${roots[@]}" -name '*.cs' \
    -not -path '*/obj/*' -not -path '*/bin/*' \
    -newer "$ref" -print -quit 2>/dev/null
}

# Strip the known-benign LEADING prefixes so what remains begins with the real command:
# chained `cd …&&` / `export …&&`, env-assignments, an optional `rtk` wrapper. Regex can't
# parse shell, so we only ever strip these safe leaders — never anything that could hide a
# different command.
strip_cmd_prefixes() {
  printf '%s' "$1" | sed -E '
    s/^[[:space:]]+//
    :a; s/^(cd|export)[[:space:]]+[^&]*&&[[:space:]]*//; ta
    :b; s/^[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]]+//; tb
    s/^rtk[[:space:]]+//
  '
}

# True only when `dotnet test` is the actual command being run — not text inside an
# argument (a commit message, an echo string, quoted data that merely mentions it). After
# prefix-stripping, require what remains to START with `dotnet test` at a word boundary
# (so `dotnet testfoo` and `git commit -m "…dotnet test…"` do not match).
is_dotnet_test() {
  strip_cmd_prefixes "$1" | grep -Eq '^dotnet[[:space:]]+test([[:space:]]|$)'
}

# Echo only the leading `dotnet test …` invocation — the segment up to the first chained
# command (&&, ||, ;, |, &) — so a `--no-build` sitting in a trailing `echo` or a quoted
# argument can't be mistaken for a flag belonging to `dotnet test`. Empty if not a real
# `dotnet test` run.
dotnet_test_invocation() {
  is_dotnet_test "$1" || return 0
  strip_cmd_prefixes "$1" | sed -E 's/[[:space:]]*(&&|\|\||[;&|]).*$//'
}

emit_pre_deny() { # reason
  jq -n --arg r "$1" '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
  exit 0
}

case "$EVENT" in
# ---------------------------------------------------------------------------------
# deny `dotnet test --no-build` against a stale build.
PreToolUse)
  [ "$TOOL" = "Bash" ] || exit 0
  CMD=$(jget '.tool_input.command // empty')
  INVOKE=$(dotnet_test_invocation "$CMD")
  [ -n "$INVOKE" ] || exit 0
  # --no-build must be a flag of the `dotnet test` invocation itself, not a substring in a
  # chained command or a quoted argument — check the extracted invocation, not raw $CMD.
  printf '%s' "$INVOKE" | grep -Eq -- '--no-build' || exit 0

  # Newest built test assembly is the reference "last build".
  newest_dll=$(find "$ROOT/src" -path '*/bin/*' -name '*.Tests.dll' -printf '%T@ %p\n' 2>/dev/null \
                 | sort -n | tail -1 | cut -d' ' -f2-)
  if [ -z "$newest_dll" ]; then
    emit_pre_deny "No built test assemblies found, but --no-build was requested. Build first (drop --no-build), then re-run. (feedback_dotnet_test_no_build)"
  fi
  newer=$(first_newer_than "$newest_dll")
  if [ -n "$newer" ]; then
    emit_pre_deny "Source changed since the last build (e.g. ${newer#"$ROOT"/}). 'dotnet test --no-build' would run a STALE assembly and report misleading results. Rebuild first (drop --no-build). (feedback_dotnet_test_no_build)"
  fi
  exit 0
  ;;
esac

exit 0
