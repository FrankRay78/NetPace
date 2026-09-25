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
# Reopening a real session and a real end-to-end run need a real model, so they are checked by
# hand rather than faked here; docs/agentic-workflow.md (*The chain script*) records how. That is the one
# scenario with no marker below — "A failed stage can be reopened".
#
#   Usage:  scripts/chain.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHAIN="$HERE/chain.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
# A developer's ambient overrides would otherwise reach every case; the stall case sets its own.
unset CHAIN_MODEL CHAIN_STAGE_TIMEOUT

# Anything on the chain's stdin that reaches the stub proves the stage's own `</dev/null` is
# gone — the omission chain.sh records as making an unattended call stall on the terminal.
printf 'this must never reach a stage\n' > "$SB/stdin-data"

# The stub claude. One log line per invocation, arguments separated by `|` so a lost quote is
# visible as a new field rather than hidden by space-joining. The reply is the canned JSON the
# case wrote for that call number. A sleep-<n> file makes call n record its PID and sleep
# instead — exec, so the recorded PID is the sleeping process itself.
mkdir -p "$SB/bin"
cat > "$SB/bin/claude" <<'STUB'
#!/usr/bin/env bash
{ for a in "$@"; do printf '%s|' "$a"; done; printf '\n'; } >> "$STUB_DIR/log"
if IFS= read -r -t 0.1 _ <&0 2>/dev/null; then printf 'leaked\n' >> "$STUB_DIR/stdin-leak"; fi
n=$(grep -c '' "$STUB_DIR/log")
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

# chain <args…> — run the chain from inside the case repo. Its stdin carries data on purpose;
# see stdin-data above. Sets OUTPUT (stdout and stderr) and RC, which every assertion reads.
chain() { OUTPUT="$(cd "$REPO" && PATH="$SB/bin:$PATH" bash "$CHAIN" "$@" <"$SB/stdin-data" 2>&1)"; RC=$?; }

# grep -c '' rather than `wc -l`, whose output is right-padded on BSD/macOS (Principle IV).
calls() { if [ -f "$STUB_DIR/log" ]; then grep -c '' "$STUB_DIR/log"; else echo 0; fi; }
call() { sed -n "$1p" "$STUB_DIR/log" 2>/dev/null; }
repo_state() { git -C "$REPO" rev-parse HEAD; git -C "$REPO" rev-parse --abbrev-ref HEAD; git -C "$REPO" status --porcelain; }

# The prompt on call n is this, and it is the single positional straight after -p. The position
# is pinned deliberately: chain.sh records (from plugin-report.sh) that any other placement makes
# the call stall, and the `|` field separator is what makes a lost quote visible here.
prompt_is() { call "$1" | grep -qF -- "-p|$2|"; }

# The chain's closing message: its last two lines, after any stage report.
closing() { printf '%s\n' "$OUTPUT" | tail -n 2; }
# The single closing line, so an assertion cannot be satisfied by the echoed stage report.
last_line() { printf '%s\n' "$OUTPUT" | tail -n 1; }

# // SCENARIO: One issue to a pull request
echo "One issue to a pull request:"
new_case
reply 1 $'Built.\nREADY branch=feature/270-x\nDone.'
reply 2 'STUDIED issue=270 rows=0'
reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 $'Related work: https://github.com/o/r/pull/1\nRAISED pr=https://github.com/o/r/pull/9'
CHAIN_MODEL=test-model chain 270
ok "exits 0" '[ "$RC" = 0 ]'
ok "exactly five stages started" '[ "$(calls)" = 5 ]'
ok "stages in order: build, study, verify, study, raise-pr" 'prompt_is 1 "/build 270" && prompt_is 2 "/study 270" && prompt_is 3 "/verify" && prompt_is 4 "/study 270" && prompt_is 5 "/raise-pr 270"'
ok "first study resumes build's session" 'call 2 | grep -qF -- "--resume|sess-1|"'
ok "second study resumes verify's session" 'call 4 | grep -qF -- "--resume|sess-3|"'
ok "build, verify and raise-pr start fresh sessions" '[ "$(calls)" = 5 ] && ! call 1 | grep -qF -- "--resume|" && ! call 3 | grep -qF -- "--resume|" && ! call 5 | grep -qF -- "--resume|"'
ok "every stage uses the one chosen model" '[ "$(grep -cF -- "--model|test-model|" "$STUB_DIR/log" 2>/dev/null)" = 5 ]'
ok "no stage is asked to prompt for permission" '[ "$(grep -cF -- "--dangerously-skip-permissions|" "$STUB_DIR/log" 2>/dev/null)" = 5 ]'
ok "no stage can read the terminal" '[ ! -f "$STUB_DIR/stdin-leak" ]'
ok "the closing line names the pull request that was raised" 'last_line | grep -qF "chain: done — https://github.com/o/r/pull/9"'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "with no override every stage still names the default model" '[ "$RC" = 0 ] && [ "$(grep -cF -- "--model|claude-opus-5|" "$STUB_DIR/log")" = 5 ]'
new_case
reply 1 'READY branch=feature/270-x'
chain "#270"
ok "an issue written #270 is the same issue" 'prompt_is 1 "/build 270"'

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
ok "closing message names the session to reopen" 'closing | grep -qF "claude --resume sess-3"'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'
new_case
reply 1 $'READY branch=feature/270-x\nFAILED reason=half built'
chain 270
ok "a FAILED verdict outranks a success verdict in the same report" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "half built"'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 $'STUDIED issue=270 rows=1\nRow 1 records that the build stage can print FAILED reason=<text> and stop.'
reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "a study pass that quotes 'FAILED reason=' mid-sentence does not stop the run" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ]'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-3.json"
chain 270
ok "a stage that fails before its reply is read names no earlier stage's session" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && ! closing | grep -q "sess-2" && closing | grep -q "no session id was captured"'
new_case
reply 1 'READY branch=feature/270-x'
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-2.json"
chain 270
ok "a resuming stage that fails that early names the session it resumed" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && closing | grep -qF "claude --resume sess-1"'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
echo 30 > "$STUB_DIR/sleep-3"
CHAIN_STAGE_TIMEOUT=1 chain 270
ok "a stalled stage names no earlier stage's session either" '[ "$RC" = 1 ] && ! closing | grep -q "sess-2" && closing | grep -q "no session id was captured"'

# // SCENARIO: A stage with no readable verdict is a failure
echo "A stage with no readable verdict is a failure:"
new_case
reply 1 'I finished.'
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build and the missing verdict" 'closing | grep -q build && closing | grep -q "no recognisable verdict"'
new_case
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-1.json"
chain 270
ok "a reply that is not JSON says so, and shows the reply" '[ "$RC" = 1 ] && closing | grep -q "not JSON" && grep -qF "command failed before it could start" <<<"$OUTPUT"'
new_case
jq -n '{type:"result",subtype:"error_during_execution",is_error:true,result:"API Error: 529 overloaded",session_id:"sess-1"}' > "$STUB_DIR/reply-1.json"
chain 270
ok "an errored reply is reported as the error, not as a missing verdict" '[ "$RC" = 1 ] && grep -q "529 overloaded" <<<"$OUTPUT" && ! closing | grep -q "no recognisable verdict"'
new_case
jq -n '{type:"result",subtype:"success",is_error:false,result:"READY branch=feature/270-x"}' > "$STUB_DIR/reply-1.json"
chain 270
ok "a reply with no session id stops rather than studying a fresh session" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && grep -q "session id" <<<"$OUTPUT"'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 'A pull request for this branch already exists: https://github.com/o/r/pull/8'
chain 270
ok "a mentioned pull request is not a raised one" '[ "$RC" = 1 ] && closing | grep -q "no recognisable verdict" && ! last_line | grep -q "chain: done"'

# // SCENARIO: A stalled stage ends the run
echo "A stalled stage ends the run:"
new_case
echo 30 > "$STUB_DIR/sleep-1"
CHAIN_STAGE_TIMEOUT=1 chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build as stalled" 'closing | grep -q build && closing | grep -q stalled'
ok "the stalled process was ended" '[ -s "$STUB_DIR/pid-1" ] && ! kill -0 "$(cat "$STUB_DIR/pid-1")" 2>/dev/null'

# // SCENARIO: Wrong starting point is refused
echo "Wrong starting point is refused:"
new_case
before="$(repo_state)"
chain
ok "no issue: exits 1 with usage, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q usage <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
before="$(repo_state)"
chain abc
ok "an issue that is not a number: exits 1 with usage, no stage started" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q usage <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
touch "$REPO/stray.txt"
before="$(repo_state)"
chain 270
ok "dirty tree: exits 1 naming the dirty tree, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "not clean" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
git -C "$REPO" checkout -q -b feature/x
before="$(repo_state)"
chain 270
ok "not on main: exits 1 naming the branch, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "feature/x" <<<"$OUTPUT" && grep -q "not main" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
OUTPUT="$(cd "$SB" && PATH="$SB/bin:$PATH" bash "$CHAIN" 270 <"$SB/stdin-data" 2>&1)"; RC=$?
ok "outside a repository: refuses naming git, rather than reading it as a clean tree" '[ "$RC" = 1 ] && grep -q "could not read this repository" <<<"$OUTPUT" && [ "$(calls)" = 0 ]'

# // SCENARIO: Asked what it would do
echo "Asked what it would do:"
new_case
before="$(repo_state)"
chain --dry-run 270
ok "exits 0" '[ "$RC" = 0 ]'
ok "lists the five commands in run order" '[ "$(grep -oE "/(build|study|verify|raise-pr)( 270)?" <<<"$OUTPUT" | paste -sd,)" = "/build 270,/study 270,/verify,/study 270,/raise-pr 270" ]'
ok "no stage started" '[ "$(calls)" = 0 ]'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'
new_case
touch "$REPO/stray.txt"
before="$(repo_state)"
chain --dry-run 270
ok "a dirty tree is still told what would run" '[ "$RC" = 0 ] && [ "$(calls)" = 0 ] && [ "$(repo_state)" = "$before" ]'
new_case
git -C "$REPO" checkout -q -b feature/x
before="$(repo_state)"
chain --dry-run 270
ok "a feature branch is still told what would run" '[ "$RC" = 0 ] && [ "$(calls)" = 0 ] && [ "$(repo_state)" = "$before" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
