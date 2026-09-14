#!/usr/bin/env bash
#
# chain.tests.sh — standalone test matrix for chain.sh.
#
# The chain's one job is gating: which stage starts, in what order, resuming which session, and
# what happens when a stage fails. All of that is provable from outside with a stub `claude`
# first on PATH, so no model is called and nothing is spent. Every case runs the chain inside a
# throwaway git repo under a mktemp sandbox, so your checkout is never touched. Exits non-zero
# on any failure. Run it after any edit to the chain.
#
# Reopening a real session and a real end-to-end run need a real model; those are checked by
# hand (specs quickstart §4 and §5), not faked here.
#
#   Usage:  scripts/chain.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHAIN="$HERE/chain.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
unset CHAIN_MODEL CHAIN_STAGE_TIMEOUT

# The stub claude. One log line per invocation (its full argument list); the reply is the
# canned JSON the case wrote for that call number. A sleep-<n> file makes call n record its PID
# and sleep instead — exec, so the recorded PID is the sleeping process itself.
mkdir -p "$SB/bin"
cat > "$SB/bin/claude" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_DIR/log"
n=$(wc -l < "$STUB_DIR/log")
if [ -f "$STUB_DIR/sleep-$n" ]; then
  echo $$ > "$STUB_DIR/pid-$n"
  exec sleep "$(cat "$STUB_DIR/sleep-$n")"
fi
cat "$STUB_DIR/reply-$n.json"
STUB
chmod +x "$SB/bin/claude"

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# A fresh repo on main with one commit, and an empty stub directory, per case.
new_case() {
  cases=$((cases+1))
  REPO="$SB/case$cases/repo"
  export STUB_DIR="$SB/case$cases/stub"
  mkdir -p "$REPO" "$STUB_DIR"
  git -C "$REPO" init -q -b main
  git -C "$REPO" config user.name chain-tests
  git -C "$REPO" config user.email chain-tests@example.invalid
  git -C "$REPO" config commit.gpgsign false
  git -C "$REPO" commit -q --allow-empty -m init
}

# reply <n> <result text> — the JSON reply for call n.
reply() {
  jq -n --arg r "$2" --arg s "sess-$1" \
    '{type:"result",subtype:"success",is_error:false,result:$r,session_id:$s}' > "$STUB_DIR/reply-$1.json"
}

# chain <args…> — run the chain from inside the case repo with no terminal input.
chain() { OUTPUT="$(cd "$REPO" && PATH="$SB/bin:$PATH" bash "$CHAIN" "$@" </dev/null 2>&1)"; RC=$?; }

calls() { if [ -f "$STUB_DIR/log" ]; then wc -l < "$STUB_DIR/log"; else echo 0; fi; }
call() { sed -n "$1p" "$STUB_DIR/log" 2>/dev/null; }
repo_state() { git -C "$REPO" rev-parse HEAD; git -C "$REPO" rev-parse --abbrev-ref HEAD; git -C "$REPO" status --porcelain; }

# The prompt sent on call n is exactly this command (argument order: -p "<prompt>" first).
prompt_is() { call "$1" | grep -qE -- "^-p $2( |\$)"; }

# // SCENARIO: One issue to a pull request
echo "One issue to a pull request:"
new_case
reply 1 $'Built.\nREADY branch=feature/270-x\nDone.'
reply 2 'STUDIED issue=270 rows=0'
reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 'Opened https://github.com/o/r/pull/9'
CHAIN_MODEL=test-model chain 270
ok "exits 0" '[ "$RC" = 0 ]'
ok "exactly five stages started" '[ "$(calls)" = 5 ]'
ok "stages in order: build, study, verify, study, raise-pr" 'prompt_is 1 "/build 270" && prompt_is 2 "/study 270" && prompt_is 3 "/verify" && prompt_is 4 "/study 270" && prompt_is 5 "/raise-pr 270"'
ok "first study resumes build's session" 'call 2 | grep -q -- "--resume sess-1"'
ok "second study resumes verify's session" 'call 4 | grep -q -- "--resume sess-3"'
ok "build, verify and raise-pr start fresh sessions" '[ "$(calls)" = 5 ] && ! call 1 | grep -q -- --resume && ! call 3 | grep -q -- --resume && ! call 5 | grep -q -- --resume'
ok "every stage uses the one chosen model" '[ "$(grep -c -- "--model test-model" "$STUB_DIR/log" 2>/dev/null)" = 5 ]'
ok "reports the pull request" 'printf "%s" "$OUTPUT" | grep -qF "https://github.com/o/r/pull/9"'

# The chain's closing message: its last two lines, after any stage report.
closing() { printf '%s\n' "$OUTPUT" | tail -n 2; }

# // SCENARIO: A failing stage stops the run
echo "A failing stage stops the run:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 $'Suite failed.\nFAILED reason=suite red'
before="$(repo_state)"
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no stage after verify started" '[ "$(calls)" = 3 ]'
ok "closing message names verify, 3/5 and the reason" 'closing | grep -q verify && closing | grep -qF 3/5 && closing | grep -q "suite red"'
ok "closing message says how to reopen the session" 'closing | grep -q "claude --resume"'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'
new_case
reply 1 $'READY branch=feature/270-x\nFAILED reason=half built'
chain 270
ok "a FAILED verdict outranks a success verdict in the same report" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "half built"'

# // SCENARIO: A stage with no readable verdict is a failure
echo "A stage with no readable verdict is a failure:"
new_case
reply 1 'I finished.'
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build and the missing verdict" 'closing | grep -q build && closing | grep -q "no recognisable verdict"'

# // SCENARIO: A stalled stage ends the run
echo "A stalled stage ends the run:"
new_case
echo 30 > "$STUB_DIR/sleep-1"
CHAIN_STAGE_TIMEOUT=1 chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build as stalled" 'closing | grep -q build && closing | grep -q stalled'
ok "the stalled process was ended" '[ -s "$STUB_DIR/pid-1" ] && ! kill -0 "$(cat "$STUB_DIR/pid-1")" 2>/dev/null'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
