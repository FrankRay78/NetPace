#!/usr/bin/env bash
#
# green-gate.tests.sh — standalone synthetic-JSON test matrix for green-gate.sh.
#
# The hook is a single PreToolUse(Bash) gate, so every branch is provable by piping
# synthetic hook JSON — no dev stack required. This script drives all branches against a
# throwaway CLAUDE_PROJECT_DIR so the real repo is never touched, and exits non-zero on any
# failure. Run it after any edit to the hook.
#
# (History: the hook once also carried PostToolUse ledger-stamping and a Stop completion
# gate; both were retired in issue #122 when test-green enforcement moved to `/ship`. Those
# cases are gone from this matrix; only the marker-independent B7 --no-build deny remains.)
#
#   Usage:  .claude/hooks/green-gate.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/green-gate.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
export CLAUDE_PROJECT_DIR="$SB"
mkdir -p "$SB/src/Svc" "$SB/src/Svc.Tests/bin/Debug"
DLL="$SB/src/Svc.Tests/bin/Debug/Svc.Tests.dll"

pass=0; fail=0
run() { OUTPUT="$(printf '%s' "$1" | "$HOOK" 2>/dev/null)"; RC=$?; }
ok()  { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- got:[$OUTPUT] rc=$RC"; fail=$((fail+1)); fi; }

# Synthetic-payload builder (keep the real hook schema in one place).
pre()  { printf '{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":%s}}' "$(jq -Rn --arg c "$1" '$c')"; }

echo "PreToolUse / B7 (--no-build staleness):"
run "$(printf '{"hook_event_name":"PreToolUse","tool_name":"Edit","tool_input":{"file_path":"x.cs"}}')"; ok "non-Bash tool → allow" '[ -z "$OUTPUT" ] && [ "$RC" = 0 ]'
run "$(pre 'echo dotnet test --no-build')";        ok "echo mention → allow" '[ -z "$OUTPUT" ]'
run "$(pre 'dotnet test')";                         ok "dotnet test (build) → allow" '[ -z "$OUTPUT" ]'
run "$(pre 'dotnet test --no-build')";              ok "no dll → deny" 'echo "$OUTPUT"|jq -e ".hookSpecificOutput.permissionDecision==\"deny\"">/dev/null'
touch "$SB/src/Svc/A.cs"; sleep 0.05; : > "$DLL"
run "$(pre 'dotnet test --no-build')";              ok "fresh build → allow" '[ -z "$OUTPUT" ]'
sleep 0.05; touch "$SB/src/Svc/A.cs"
run "$(pre 'dotnet test --no-build')";              ok "stale source → deny" 'echo "$OUTPUT"|jq -e ".hookSpecificOutput.permissionDecision==\"deny\"">/dev/null'
run "$(pre 'cd /repo && dotnet test --no-build')";  ok "prefix cd&& stale → deny" 'echo "$OUTPUT"|jq -e ".hookSpecificOutput.permissionDecision==\"deny\"">/dev/null'
run "$(pre 'git commit -m "run dotnet test --no-build later"')"; ok "commit msg mention → allow" '[ -z "$OUTPUT" ]'
# regression: --no-build in a chained command must NOT deny a plain `dotnet test` (fresh build)
sleep 0.05; : > "$DLL"
run "$(pre 'dotnet test --filter X && echo try --no-build next')"; ok "--no-build in chained echo → allow" '[ -z "$OUTPUT" ]'
# a generated obj/ source newer than the DLL must NOT deny (only real *.cs under src/ count)
sleep 0.05; mkdir -p "$SB/src/Svc/obj"; touch "$SB/src/Svc/obj/Gen.cs"
run "$(pre 'dotnet test --no-build')";              ok "newer obj/*.cs ignored → allow" '[ -z "$OUTPUT" ]'

echo "fail-open / override:"
run ''; ok "empty stdin → allow" '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
OUTPUT="$(printf '%s' "$(pre 'dotnet test --no-build')" | NETPACE_SKIP_GREEN_GATE=1 "$HOOK" 2>"$SB/err")"; RC=$?
ok "NETPACE_SKIP_GREEN_GATE=1 → no-op + warns" '[ -z "$OUTPUT" ] && grep -q BYPASSED "$SB/err"'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
