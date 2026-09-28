#!/usr/bin/env bash
#
# no-chmod.tests.sh — standalone test matrix for no-chmod.sh.
#
# The hook is a single PreToolUse(Bash) gate over the command string, so every branch is provable
# by piping synthetic hook JSON — no dev stack and no filesystem fixture required. The `--check`
# block near the end drives the second mode through CLI arguments instead, and the wiring block
# reads `.claude/settings.json`. Exits non-zero on any failure. Run it after any edit to the hook.
#
# The matrix is the gate's RED-GREEN evidence (Constitution §I, configuration/tooling): it is
# the real tool that decides whether the matcher blocks what it must and waves through what it
# must not, and it runs in CI via shell-tests.yml.
#
#   Usage:  bash .claude/hooks/no-chmod.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/no-chmod.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

pass=0; fail=0
# Stderr is captured, not discarded: an internal shell error (unbound variable, a missing tool, a
# broken regex) otherwise leaves no trace, and combined with a stdout-only allow check a hook that
# errored on every call would pass every allow assertion.
run() { OUTPUT="$(printf '%s' "$1" | bash "$HOOK" 2>"$TMP/err")"; RC=$?; ERR="$(cat "$TMP/err")"; }
ok()  { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- got:[$OUTPUT] rc=$RC err=[${ERR:-}]"; fail=$((fail+1)); fi; }

# Synthetic-payload builder (keep the real hook schema in one place).
pre() { printf '{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":%s}}' "$(jq -Rn --arg c "$1" '$c')"; }

decision() { echo "$OUTPUT" | jq -r '.hookSpecificOutput.permissionDecision' 2>/dev/null; }
reason()   { echo "$OUTPUT" | jq -r '.hookSpecificOutput.permissionDecisionReason' 2>/dev/null; }
denied()   { [ "$RC" = 0 ] && [ "$(decision)" = "deny" ]; }
# Allowing is exit 0 AND no decision payload AND nothing on stderr — all three, because any one of
# them alone passes for a hook that blocks everything. Under the PreToolUse contract exit 2 with
# empty stdout BLOCKS the call, so a stdout-only check reported `ok` for a stub hook that denied
# every Bash call in the repo — including `bash t.sh`, the alternative this gate recommends.
allowed()  { [ "$RC" = 0 ] && [ -z "$OUTPUT" ] && [ -z "$ERR" ]; }

echo "executable-bit forms → deny:"
for c in \
  'chmod +x t.sh' \
  'chmod u+x t.sh' \
  'chmod a+x t.sh' \
  'chmod ug+x t.sh' \
  'chmod u=rx t.sh' \
  'chmod +rwx t.sh' \
  'chmod 755 t.sh' \
  'chmod 700 t.sh' \
  'chmod 777 t.sh' \
  'chmod 4755 t.sh' \
  'chmod 7 t.sh' \
  'chmod 0755 t.sh' \
  'chmod u=rwx,go=rx t.sh' \
  'chmod -R +x scripts' \
  'chmod -Rv +x scripts' \
  'chmod --recursive +x scripts' \
  'chmod -v 755 t.sh' \
  'chmod -- +x t.sh' \
  'chmod "+x" t.sh' \
  "chmod '+x' t.sh" \
  'chmod +x "my script.sh"' \
  'chmod g-w,u+x t.sh'
do
  run "$(pre "$c")"; ok "$c" denied
done

echo "non-executable chmod forms → allow (stay on the ask rule):"
for c in \
  'chmod 644 t.sh' \
  'chmod 600 secrets.env' \
  'chmod 1644 t.sh' \
  'chmod -x t.sh' \
  'chmod a-x t.sh' \
  'chmod u+w t.sh' \
  'chmod a= t.sh' \
  'chmod g+u t.sh' \
  'chmod --reference=other t.sh' \
  'chmod --reference other t.sh'
do
  run "$(pre "$c")"; ok "$c" allowed
done

echo "segment matching (issue #293 — chmod at the start of any chained segment):"
run "$(pre 'cd /repo && chmod +x t.sh')";        ok "cd && chmod +x → deny" denied
run "$(pre 'cd /repo; chmod +x t.sh')";          ok "cd ; chmod +x → deny" denied
run "$(pre 'export FOO=1 && chmod 755 t.sh')";   ok "export && chmod 755 → deny" denied
run "$(pre 'FOO=1 chmod +x t.sh')";              ok "env-assignment prefix → deny" denied
run "$(pre 'rtk chmod +x t.sh')";                ok "rtk wrapper → deny" denied
run "$(pre 'chmod +x t.sh && ./t.sh')";          ok "chmod first, then run → deny" denied
run "$(pre 'ls -l | chmod +x t.sh')";            ok "after a pipe → deny" denied
run "$(pre 'chmod \
  +x t.sh')";                                    ok "line continuation → deny" denied
run "$(pre 'cd /repo
chmod +x t.sh
./t.sh')";                                       ok "own line of a multi-line script → deny" denied
run "$(pre 'set -e
  chmod 755 scripts/x.sh')";                     ok "indented own line → deny" denied

echo "heredoc bodies are data, not commands → allow:"
run "$(pre "git commit -q -F - <<'EOF'
Refs #293: refuse chmod +x
chmod +x t.sh is what this gate stops
EOF")";                                          ok "quoted-delimiter heredoc body → allow" allowed
run "$(pre "cat <<EOF > notes.md
chmod 755 t.sh
EOF")";                                          ok "unquoted-delimiter heredoc body → allow" allowed
run "$(pre "cat <<-EOF
  chmod +x t.sh
  EOF")";                                        ok "<<- indented terminator → allow" allowed
# ...but a real chmod after the terminator is still seen.
run "$(pre "cat <<'EOF' > t.sh
echo hi
EOF
chmod +x t.sh")";                                ok "chmod after the terminator → deny" denied

echo "text mentions are not commands → allow:"
run "$(pre 'echo "run chmod +x t.sh"')";         ok "echo mention → allow" allowed
run "$(pre 'git commit -m "do not chmod +x"')";  ok "commit message mention → allow" allowed
run "$(pre 'grep -rn "chmod +x" docs/')";        ok "grep for the text → allow" allowed
run "$(pre 'bash t.sh')";                        ok "the prescribed alternative → allow" allowed
run "$(pre 'chmodx +x t.sh')";                   ok "chmod is a prefix only → allow" allowed
run "$(pre 'git add --chmod=+x t.sh')";          ok "git add --chmod=+x → allow" allowed

# Regression (issue #293): a separator inside a quoted span must not manufacture a segment head.
# The gate's first live call refused itself on a command whose only chmod sat inside quoted JSON.
run "$(pre 'echo "cd /repo && chmod +x t.sh"')"; ok "quoted && before chmod → allow" allowed
run "$(pre "echo 'cd /repo && chmod +x t.sh'")"; ok "single-quoted && before chmod → allow" allowed
run "$(pre 'git commit -m "no cd x; chmod 755 y"')"; ok "quoted ; in a commit message → allow" allowed
run "$(pre 'printf %s "{\"command\":\"cd /r && chmod +x t.sh\"}" | jq .')"; ok "quoted JSON payload → allow" allowed
run "$(pre 'grep -rn "x && chmod +x" docs/')";   ok "grep for the chained form → allow" allowed
run "$(pre 'echo "don'"'"'t && chmod +x f"')";   ok "apostrophe inside double quotes → allow" allowed
# ...but a real chained chmod alongside quoted text is still refused.
run "$(pre 'echo "note: chained" && chmod +x t.sh')"; ok "quoted text then real chmod → deny" denied

# Regression (issue #293, review): a NEWLINE inside a quoted span is not a segment boundary either.
# The quote mask covered `&&`, `;` and `|` but not the newline, so a multi-line commit message
# written *about* this rule had its second line read as an invocation — the gate refusing the very
# commit that documents it. This is the same root cause as the block above, one separator missed.
echo "a newline inside a quoted span is not a segment boundary → allow:"
run "$(pre 'git commit -m "Refs #293: refuse it
chmod +x t.sh is the gated call"')";             ok "multi-line quoted commit message → allow" allowed
run "$(pre 'printf "%s" "line one
chmod 755 y"')";                                 ok "multi-line quoted printf → allow" allowed
# ...and a real chmod after the quoted span closes is still seen.
run "$(pre 'git commit -m "a
b" && chmod +x t.sh')";                          ok "multi-line quote then real chmod → deny" denied

# Regression (issue #293, review): three forms that made the heredoc/quote walk swallow every
# FOLLOWING line, hiding a real chmod on one of them. All were silent misses, the direction the
# masking comment claimed could not happen.
echo "a false heredoc opener must not swallow the rest of the command → deny:"
run "$(pre 'grep chmod <<< "text"
chmod +x t.sh')";                                ok "herestring is not a heredoc → deny" denied
run "$(pre 'echo "a << EOF b"
chmod +x t.sh')";                                ok "<< inside quotes is not a heredoc → deny" denied
run "$(pre 'echo $((1 << 3))
chmod +x t.sh')";                                ok "arithmetic left-shift → deny" denied
run "$(pre 'echo $'"'"'a\'"'"'b'"'"' && chmod +x f')"; ok "\$'...' escaped quote → deny" denied

# Regression (issue #293, review): a mode longer than four octal digits missed the numeric branch,
# fell through to the symbolic loop, found no `+`/`=` and answered "not executable" — a wrong
# answer rather than a fail-open, while plain `0755` was refused.
echo "octal modes of any length → deny:"
for c in 'chmod 04755 f' 'chmod 00755 f' 'chmod 010755 f'; do
  run "$(pre "$c")"; ok "$c" denied
done

# Regression (issue #293, review): a wrapper or compound-body keyword left the segment not starting
# with `chmod`, so the gate declined silently. `do chmod +x "$f"` is how an agent makes a set of
# scripts executable and `sudo chmod +x` is the natural retry after a permission error, so these
# are the forms that actually cost a run.
echo "wrapper and compound-body prefixes → deny:"
for c in \
  'sudo chmod +x f' \
  '/bin/chmod +x f' \
  '\chmod +x f' \
  'command chmod +x f' \
  'env chmod +x f' \
  'exec chmod +x f' \
  'time chmod +x f' \
  'nohup chmod +x f' \
  'if true; then chmod +x f; fi' \
  'for f in *.sh; do chmod +x "$f"; done' \
  'while read f; do chmod +x "$f"; done' \
  '(chmod +x f)' \
  'true; { chmod +x f; }'
do
  run "$(pre "$c")"; ok "$c" denied
done

# KNOWN LIMITS — decided and declined, not overlooked. These reach `chmod` through a token that is
# not the segment head, which needs argument parsing rather than a head match. They stay fail-open
# and governed by the `Bash(chmod:*)` ask rule; recorded here so a reader can tell a declined case
# from an unconsidered one.
echo "out of scope by decision → allow (still on the ask rule):"
for c in \
  'find . -name "*.sh" -exec chmod +x {} \;' \
  'echo t.sh | xargs chmod +x' \
  'bash -c "chmod +x f"'
do
  run "$(pre "$c")"; ok "$c" allowed
done
# An unbalanced quote holds the quote level open to end of input, merging segments. Fail-open by
# construction: the walk can only ever MERGE, so an unmodelled quoting form is a miss, never a
# false refusal.
run "$(pre 'echo "unbalanced && chmod +x f')";   ok "unbalanced quote → allow" allowed

# SCENARIO: A gated call becomes a redirection, not a dead end
echo "deny message names the alternative:"
run "$(pre 'chmod +x t.sh')"
ok "reason mentions 'bash'" 'reason | grep -q "bash "'
ok "reason mentions the script form" 'reason | grep -q "bash script.sh"'

# SCENARIO: An unattended run is not stalled by it
# The decision must be `deny` — a settled outcome the run carries on from — and never `ask`,
# which is the outcome that stalls an interactive run and vanishes in a headless one. This is the
# observable property that keeps an unattended chain moving rather than waiting on a human.
echo "the decision is settled, not deferred to a human:"
run "$(pre 'chmod +x t.sh')"
ok "decision is deny" denied
ok "decision is never ask" '[ "$(decision)" != "ask" ]'
ok "hook exits 0 (the decision travels in the payload, not the exit code)" '[ "$RC" = 0 ]'
# The harness reads the decision out of a specific envelope. If `hookEventName` were dropped or
# misspelled the decision would be ignored entirely and the gate would stop working with every
# other assertion still green, so pin the shape once rather than only the field inside it.
ok "payload carries the full PreToolUse envelope" \
  'echo "$OUTPUT" | jq -e ".hookSpecificOutput | .hookEventName == \"PreToolUse\" and has(\"permissionDecision\") and (.permissionDecisionReason | length > 0)" >/dev/null'

# The gate fires on EVERY Bash call, so its own cost is part of its contract: a slow gate on a long
# command reintroduces exactly the stall #293 exists to remove. The quote walk used to be quadratic
# — a 26 KB `gh pr create --body "…"` mentioning `chmod` took 12 s to classify. `timeout` is the
# assertion, so this needs no clock arithmetic and cannot be flaky about a few ms.
echo "classification stays bounded on a long command:"
BIG="gh pr create --body \"documents the chmod rule $(head -c 26000 /dev/zero | tr '\0' a)\""
OUTPUT="$(timeout 2 bash "$HOOK" --check "$BIG" 2>&1)"; RC=$?
ok "26 KB quoted body classifies well inside 2 s" '[ "$RC" = 0 ]'
BIG="$BIG && chmod +x t.sh"
OUTPUT="$(timeout 2 bash "$HOOK" --check "$BIG" 2>&1)"; RC=$?
ok "...and still refuses a real chmod chained onto it" '[ "$RC" = 1 ]'

# The wiring is a litigated decision, not an incidental: the CIR records both halves, and either one
# silently un-gates the rule if edited away. Nothing else in the matrix reads settings.json.
echo "settings.json wiring (both halves are recorded decisions):"
SETTINGS="$HERE/../settings.json"
ok "hook is registered on PreToolUse(Bash) without an \`if\` clause" \
  'jq -e "[.hooks.PreToolUse[] | select(.matcher == \"Bash\") | .hooks[] | select(.command | contains(\"no-chmod.sh\"))] | length == 1 and (.[0] | has(\"if\") | not)" "$SETTINGS" >/dev/null'
ok "\`Bash(chmod:*)\` stays on permissions.ask as the fail-open backstop" \
  'jq -e ".permissions.ask | index(\"Bash(chmod:*)\") != null" "$SETTINGS" >/dev/null'

echo "--check mode (classify one command string):"
OUTPUT="$(bash "$HOOK" --check 'chmod +x t.sh' 2>&1)"; RC=$?
ok "--check on a blocked form → exit 1 + message" '[ "$RC" = 1 ] && echo "$OUTPUT" | grep -q "bash script.sh"'
OUTPUT="$(bash "$HOOK" --check 'chmod 644 t.sh' 2>&1)"; RC=$?
ok "--check on an allowed form → exit 0" '[ "$RC" = 0 ]'
OUTPUT="$(bash "$HOOK" --check 2>/dev/null)"; RC=$?
ERRTXT="$(bash "$HOOK" --check 2>&1 >/dev/null)"
ok "--check with no command → usage on stderr, exit 2" '[ "$RC" = 2 ] && [ -z "$OUTPUT" ] && echo "$ERRTXT" | grep -q usage'
OUTPUT="$(NETPACE_ALLOW_CHMOD=1 bash "$HOOK" --check 'chmod +x t.sh' 2>&1)"; RC=$?
ok "--check ignores the override" '[ "$RC" = 1 ]'

echo "fail-open / override:"
run ''; ok "empty stdin → allow" '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
run 'not json at all'; ok "unparseable payload → allow" '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
run "$(printf '{"hook_event_name":"PreToolUse","tool_name":"Edit","tool_input":{"file_path":"x.cs"}}')"
ok "non-Bash tool → allow" allowed
run "$(printf '{"hook_event_name":"PostToolUse","tool_name":"Bash","tool_input":{"command":"chmod +x t.sh"}}')"
ok "non-PreToolUse event → allow" allowed
# These paths exit BEFORE draining stdin, so the payload is redirected from a file rather than piped
# in: under `pipefail` a pipe would report the writer's SIGPIPE (141) instead of the hook's own code,
# and the hook's exit code is the thing under test.
pre 'chmod +x t.sh' > "$TMP/deny.json"

OUTPUT="$(NETPACE_ALLOW_CHMOD=1 bash "$HOOK" < "$TMP/deny.json" 2>"$TMP/bypass.err")"; RC=$?
ok "NETPACE_ALLOW_CHMOD=1 → no-op + warns" '[ "$RC" = 0 ] && [ -z "$OUTPUT" ] && grep -q BYPASSED "$TMP/bypass.err"'

# An unenforced gate must never be silently in effect — the same standard the override above holds
# itself to. Behaviour stays fail-open (exit 0); only the announcement is asserted. Each PATH holds
# every tool the hook needs EXCEPT the one under test, and bash is invoked by absolute path so the
# stripped PATH cannot hide the interpreter itself.
BASHBIN="$(command -v bash)"
for missing in jq awk; do
  mkdir -p "$TMP/no-$missing"
  for tool in cat jq awk; do
    [ "$tool" = "$missing" ] && continue
    ln -sf "$(command -v "$tool")" "$TMP/no-$missing/$tool"
  done
  OUTPUT="$(PATH="$TMP/no-$missing" "$BASHBIN" "$HOOK" < "$TMP/deny.json" 2>"$TMP/no-$missing.err")"; RC=$?
  ok "missing $missing → allows but says so on stderr" \
    '[ "$RC" = 0 ] && [ -z "$OUTPUT" ] && grep -q "NOT enforced" "$TMP/no-'"$missing"'.err"'
done

# A decision already made must not be lost. If the payload cannot be written, exit 0 would ALLOW a
# call the gate had just judged refusable, so this one case fails CLOSED via the blocking exit code.
OUTPUT="$(printf '%s' "$(pre 'chmod +x t.sh')" | bash "$HOOK" 2>"$TMP/full.err" >/dev/full)"; RC=$?
ok "unwritable stdout → blocking exit 2 + reason on stderr" \
  '[ "$RC" = 2 ] && grep -q "bash script.sh" "$TMP/full.err"'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
