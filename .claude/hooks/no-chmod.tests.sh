#!/usr/bin/env bash
#
# no-chmod.tests.sh — standalone synthetic-JSON test matrix for no-chmod.sh.
#
# The hook is a single PreToolUse(Bash) gate over the command string, so every branch is
# provable by piping synthetic hook JSON — no dev stack and no filesystem fixture required.
# Exits non-zero on any failure. Run it after any edit to the hook.
#
# The matrix is the gate's RED-GREEN evidence (Constitution §I, configuration/tooling): it is
# the real tool that decides whether the matcher blocks what it must and waves through what it
# must not, and it runs in CI via shell-tests.yml.
#
#   Usage:  bash .claude/hooks/no-chmod.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/no-chmod.sh"

pass=0; fail=0
run() { OUTPUT="$(printf '%s' "$1" | bash "$HOOK" 2>/dev/null)"; RC=$?; }
ok()  { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- got:[$OUTPUT] rc=$RC"; fail=$((fail+1)); fi; }

# Synthetic-payload builder (keep the real hook schema in one place).
pre() { printf '{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":%s}}' "$(jq -Rn --arg c "$1" '$c')"; }

denied()  { echo "$OUTPUT" | jq -e '.hookSpecificOutput.permissionDecision=="deny"' >/dev/null; }
allowed() { [ -z "$OUTPUT" ]; }

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
  'chmod 4755 t.sh' \
  'chmod 7 t.sh' \
  'chmod -R +x scripts' \
  'chmod -v 755 t.sh' \
  'chmod "+x" t.sh' \
  "chmod '+x' t.sh" \
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
  'chmod --reference=other t.sh'
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

# SCENARIO: A gated call becomes a redirection, not a dead end
echo "deny message names the alternative:"
run "$(pre 'chmod +x t.sh')"
ok "reason mentions 'bash'" 'echo "$OUTPUT" | jq -re ".hookSpecificOutput.permissionDecisionReason" | grep -q "bash "'
ok "reason mentions the script form" 'echo "$OUTPUT" | jq -re ".hookSpecificOutput.permissionDecisionReason" | grep -q "bash script.sh"'

# SCENARIO: An unattended run is not stalled by it
# The decision must be `deny` — a settled outcome the run carries on from — and never `ask`,
# which is the outcome that stalls an interactive run and vanishes in a headless one. This is the
# observable property that keeps an unattended chain moving rather than waiting on a human.
echo "the decision is settled, not deferred to a human:"
run "$(pre 'chmod +x t.sh')"
ok "decision is deny" 'echo "$OUTPUT" | jq -e ".hookSpecificOutput.permissionDecision==\"deny\"" >/dev/null'
ok "decision is never ask" 'echo "$OUTPUT" | jq -e ".hookSpecificOutput.permissionDecision!=\"ask\"" >/dev/null'
ok "hook exits 0 (the decision travels in the payload, not the exit code)" '[ "$RC" = 0 ]'

echo "--check mode (classify one command string):"
OUTPUT="$(bash "$HOOK" --check 'chmod +x t.sh' 2>&1)"; RC=$?
ok "--check on a blocked form → exit 1 + message" '[ "$RC" = 1 ] && echo "$OUTPUT" | grep -q "bash script.sh"'
OUTPUT="$(bash "$HOOK" --check 'chmod 644 t.sh' 2>&1)"; RC=$?
ok "--check on an allowed form → exit 0" '[ "$RC" = 0 ]'
OUTPUT="$(bash "$HOOK" --check 2>&1)"; RC=$?
ok "--check with no command → usage, exit 2" '[ "$RC" = 2 ]'
OUTPUT="$(NETPACE_ALLOW_CHMOD=1 bash "$HOOK" --check 'chmod +x t.sh' 2>&1)"; RC=$?
ok "--check ignores the override" '[ "$RC" = 1 ]'

echo "fail-open / override:"
run ''; ok "empty stdin → allow" '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
run 'not json at all'; ok "unparseable payload → allow" '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
run "$(printf '{"hook_event_name":"PreToolUse","tool_name":"Edit","tool_input":{"file_path":"x.cs"}}')"
ok "non-Bash tool → allow" allowed
run "$(printf '{"hook_event_name":"PostToolUse","tool_name":"Bash","tool_input":{"command":"chmod +x t.sh"}}')"
ok "non-PreToolUse event → allow" allowed
OUTPUT="$(printf '%s' "$(pre 'chmod +x t.sh')" | NETPACE_ALLOW_CHMOD=1 bash "$HOOK" 2>"$HERE/.no-chmod.err")"; RC=$?
ok "NETPACE_ALLOW_CHMOD=1 → no-op + warns" '[ -z "$OUTPUT" ] && grep -q BYPASSED "$HERE/.no-chmod.err"'
rm -f "$HERE/.no-chmod.err"

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
