#!/usr/bin/env bash
#
# no-skipped-tests.tests.sh — standalone matrix for no-skipped-tests.sh (issue #91 B12).
#
# The gate reads the filesystem (it greps `$REPO_ROOT/src`), so a synthetic payload alone cannot
# drive it. Like its fellow filesystem-reading gates green-gate.sh and traceability-gate.sh, it
# honours `CLAUDE_PROJECT_DIR`, so every case points it at a throwaway sandbox tree and asserts on
# the real, unmodified gate. No fixture ever touches the repo's own src/.
#
# TWO THINGS THIS FILE IS CAREFUL ABOUT, both learned the hard way:
#
#   1. A case must fail when the gate breaks. Assertions therefore distinguish DETECTION
#      (blocked_detect) from FAIL-CLOSED (blocked_closed) by inspecting the message, not just the
#      exit code — both exit 2, so a bare `rc == 2` check passes against a gate stubbed to
#      `exit 2` on line 1, and a regression turning real detection into a spurious fail-closed
#      would go unseen.
#   2. The gate's exit code must be the one measured. The payload goes in by HERESTRING, never
#      `jq … | hook`: under `pipefail` a pipeline reports the rightmost non-zero status, so a jq
#      hiccup would be read as the gate's verdict.
#
# Exits non-zero on any failure. Run after any edit to the gate.
#
#   Usage:  .claude/hooks/no-skipped-tests.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/no-skipped-tests.sh"

# One parent-scoped root holds every sandbox, so cleanup cannot be defeated by subshells (a
# `TMPDIRS+=(…)` inside `$(sandbox …)` mutates a subshell's copy and leaks every fixture).
# u+rwX first: the scan-error case deliberately leaves an unreadable file behind.
ROOT="$(mktemp -d)"
cleanup() { chmod -R u+rwX "$ROOT" 2>/dev/null; rm -rf "$ROOT"; }
trap cleanup EXIT

pass=0; fail=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC out:[$(printf '%s' "$OUTPUT" | head -1)]"; fail=$((fail+1)); fi; }

# sandbox CONTENT [RELPATH] — build a throwaway project root whose src/ holds CONTENT; echo it.
# Returns empty on any setup failure; run_* below turn that into a loud FAIL rather than letting
# an empty CLAUDE_PROJECT_DIR silently send the gate at the real repo.
sandbox() {
  local t rel; t="$(mktemp -d -p "$ROOT")" || return 1
  rel="${2:-src/Sample.Tests/SampleTests.cs}"
  mkdir -p "$t/$(dirname "$rel")" || return 1
  printf '%s\n' "$1" > "$t/$rel" || return 1
  [ -s "$t/$rel" ] || return 1   # a partially-failed setup (ENOSPC/quota) must not read as "allow"
  printf '%s' "$t"
}

# empty_sandbox — a project root with NO src/ tree (fail-closed case).
empty_sandbox() { local t; t="$(mktemp -d -p "$ROOT")" || return 1; printf '%s' "$t"; }

# _drive SANDBOX PAYLOAD [ENV…] — run the gate on PAYLOAD with CLAUDE_PROJECT_DIR=SANDBOX.
_drive() {
  local sb="$1" payload="$2"; shift 2
  [ -n "$sb" ] && [ -d "$sb" ] || { OUTPUT="SANDBOX SETUP FAILED"; RC=99; return; }
  OUTPUT="$(env CLAUDE_PROJECT_DIR="$sb" "$@" "$HOOK" <<<"$payload" 2>&1)"; RC=$?
}
# run_hook SANDBOX [CMD] — hook mode with a Bash payload (default: a git commit).
run_hook() {
  local p; p="$(jq -n --arg c "${2:-git commit -m msg}" '{tool_name:"Bash",tool_input:{command:$c}}')" \
    || { OUTPUT="JQ FAILED TO BUILD PAYLOAD"; RC=98; return; }
  _drive "$1" "$p"
}
# run_raw SANDBOX RAWPAYLOAD — hook mode with a caller-supplied payload (malformed, other tools).
run_raw() { _drive "$1" "$2"; }
# run_check SANDBOX [ENV…] — --check (audit) mode.
run_check() {
  local sb="$1"; shift
  [ -n "$sb" ] && [ -d "$sb" ] || { OUTPUT="SANDBOX SETUP FAILED"; RC=99; return; }
  OUTPUT="$(env CLAUDE_PROJECT_DIR="$sb" "$@" "$HOOK" --check 2>&1)"; RC=$?
}

allowed()        { [ "$RC" = "0" ]; }
# Exit 2 covers BOTH detection and every fail-closed path, so assert on which one fired.
blocked_detect() { [ "$RC" = "2" ] && printf '%s' "$OUTPUT" | grep -q 'banned skipped-test constructs'; }
blocked_closed() { [ "$RC" = "2" ] && printf '%s' "$OUTPUT" | grep -q 'failing closed'; }

CLEAN='public class SampleTests { [Fact] public void Works() { Assert.True(true); } }'

echo "The skip family — a commit carrying one must be DETECTED and blocked:"
run_hook "$(sandbox 'class T { void M(){ Skip.If(cond, "why"); } }')";        ok "Skip.If"           'blocked_detect'
# `If` is a prefix of `IfNot` in the regex, so this asserts the OUTCOME; it cannot isolate the
# IfNot alternative (removing it from BANNED_RE leaves this green — by design, not by oversight).
run_hook "$(sandbox 'class T { void M(){ Skip.IfNot(cond, "why"); } }')";     ok "Skip.IfNot (via the If prefix)" 'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ Skip.Always("why"); } }')";          ok "Skip.Always"       'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ Skip.Unless(cond, "why"); } }')";    ok "Skip.Unless"       'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ Assert.Skip("why"); } }')";          ok "Assert.Skip"       'blocked_detect'
run_hook "$(sandbox 'class T { [SkippableFact] void M(){} }')";               ok "[SkippableFact]"   'blocked_detect'
run_hook "$(sandbox 'class T { [SkippableTheory] void M(){} }')";             ok "[SkippableTheory]" 'blocked_detect'
run_hook "$(sandbox 'class T { [Fact(Skip = "not ready")] void M(){} }')";    ok "[Fact(Skip=\"str\")]"  'blocked_detect'
run_hook "$(sandbox 'class T { [Theory(Skip="s")] void M(){} }')";            ok "[Theory(Skip=\"s\")] no-space" 'blocked_detect'
run_hook "$(sandbox 'class T { [Fact(Skip = GateReason)] void M(){} }')";     ok "[Fact(Skip=identifier)]" 'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ throw new SkipException("x"); } }')"; ok "SkipException"    'blocked_detect'
# xUnit v3's conditional-skip properties. The repo is on xunit 2.x, so these are latent — pinned
# now so a v3 migration cannot silently un-arm the gate against v3's most ergonomic skip.
run_hook "$(sandbox 'class T { [Fact(SkipUnless = nameof(StackIsUp))] void M(){} }')"; ok "[Fact(SkipUnless=…)] (xUnit v3)" 'blocked_detect'
run_hook "$(sandbox 'class T { [Fact(SkipWhen = nameof(IsCi))] void M(){} }')";       ok "[Fact(SkipWhen=…)] (xUnit v3)"   'blocked_detect'

echo ""
echo "Exclusion by PATH, not by line content (#91 H1) — the escape that let skips commit clean:"
# The bin/obj exclusion must filter on the file's PATH. Filtering grep's output instead tests the
# whole `path:lineno:content` line, so a construct whose SOURCE TEXT names /bin/ or /obj/ filters
# itself out — and a dependency-probe skip naming an absolute path is the likeliest real skip
# there is, which made the hole line up exactly with what §X most needs to catch.
run_hook "$(sandbox 'class T { void M(){ Skip.If(!File.Exists("/bin/bash"), "no bash"); } }')"; ok "Skip.If with \"/bin/…\" in the string" 'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ Assert.Skip("see /obj/notes"); } }')";                 ok "Assert.Skip with \"/obj/…\" in the string" 'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ Skip.If(x); } }' 'src/Sample.Tests/bin/Debug/G.cs')"; ok "real bin/ dir still excluded"  'allowed'
run_hook "$(sandbox 'class T { void M(){ Skip.If(x); } }' 'src/Sample.Tests/obj/Debug/G.cs')"; ok "real obj/ dir still excluded"  'allowed'
run_hook "$(sandbox 'class T { void M(){ Skip.If(x); } }' 'src/robin/objects/T.cs')";          ok "'robin'/'objects' NOT over-excluded" 'blocked_detect'

echo ""
echo "Clean tree — a commit must be ALLOWED:"
run_hook "$(sandbox "$CLEAN")";                                                ok "ordinary test file" 'allowed'
run_hook "$(sandbox 'class T { void M(){ var p = list.Skip(2).Take(3); } }')";  ok "LINQ .Skip(2) (no '=')" 'allowed'
# Differs from a banned construct ONLY by case, so it fails if the scan ever loses -E for -iE.
run_hook "$(sandbox 'class T { void M(){ assert.skip("x"); } }')";             ok "lowercase assert.skip (regex is case-sensitive)" 'allowed'
run_hook "$(sandbox 'Do not use Assert.Skip in tests.' 'src/Sample.Tests/README.md')"; ok "banned construct in a .md (only .cs is scanned)" 'allowed'

echo ""
echo "Non-commit Bash — waved through BEFORE any dependency or tree check:"
DIRTY='class T { void M(){ Skip.If(x); } }'
run_hook "$(sandbox "$DIRTY")" 'ls -la';       ok "ls with a dirty tree"       'allowed'
run_hook "$(sandbox "$DIRTY")" 'git status';   ok "git status (no 'commit')"   'allowed'
run_hook "$(sandbox "$DIRTY")" 'dotnet build'; ok "dotnet build"               'allowed'
run_hook "$(empty_sandbox)"    'ls';           ok "non-commit + no src/ (no deadlock)" 'allowed'

echo ""
echo "Multi-line commands (#91 H2) — a line-oriented match must not be dodged by a continuation:"
run_hook "$(sandbox "$DIRTY")" 'git \
  commit -m x';                                ok "git \\<newline> commit"     'blocked_detect'
run_hook "$(sandbox "$DIRTY")" 'set -e
git commit -m x';                              ok "commit on a later line"     'blocked_detect'
run_hook "$(sandbox "$DIRTY")" 'echo git
echo commit';                                  ok "'git' and 'commit' on separate lines, no commit" 'allowed'

echo ""
echo "Non-Bash tools — the tool_name check must be what saves them:"
# Each carries a real `command` field, so ONLY tool_name can allow it. Without the command field
# these pass via the pre-filter instead, and deleting the tool_name check outright goes unnoticed.
run_raw "$(sandbox "$DIRTY")" '{"tool_name":"Edit","tool_input":{"file_path":"a.md","command":"git commit -m x","new_string":"x"}}'
ok "Edit carrying a git-commit command field" 'allowed'
run_raw "$(sandbox "$DIRTY")" '{"tool_name":"Write","tool_input":{"file_path":"a.md","command":"git commit -m x","content":"x"}}'
ok "Write carrying a git-commit command field" 'allowed'

echo ""
echo "Fail-closed — for a would-be COMMIT, uncertainty must block, never wave through:"
run_hook "$(empty_sandbox)";                                        ok "commit + no src/ tree"        'blocked_closed'
run_raw  "$(sandbox "$CLEAN")" 'git commit -m "not json at all"';   ok "commit-ish, unparseable payload" 'blocked_closed'
run_raw  "$(sandbox "$CLEAN")" '{"tool_name":"Bash","tool_input":{"command":"git commit -m x"';  ok "commit-ish, truncated JSON" 'blocked_closed'
# An unreadable file makes grep error. Reading that as "clean" would be silent non-coverage, so it
# must block — and must name the path, since one bad file mode blocks EVERY commit repo-wide.
# NOTE: this case requires a non-root runner; root reads through mode 000 and it will fail. That
# is deliberate — conditioning a test on the environment is the skip pattern this very gate bans.
_sb="$(sandbox "$CLEAN")"; printf 'class L {}\n' > "$_sb/src/Sample.Tests/Locked.cs"; chmod 000 "$_sb/src/Sample.Tests/Locked.cs"
run_hook "$_sb";                                                    ok "commit + unreadable file → block" 'blocked_closed'
ok "…and the message names the offending path" 'printf "%s" "$OUTPUT" | grep -q "Locked.cs"'

echo ""
echo "Dependency bootstrap (#91 H3) — a missing tool must block a commit, never wave it through:"
# The gate's own contract says it fails closed for a commit. That held for jq but NOT for grep:
# the classifier used `grep … || exit 0`, which cannot tell "no match" from "grep is missing", so
# a broken grep allowed every commit. The classifier is now a shell builtin and grep is checked.
MINBIN="$(mktemp -d -p "$ROOT")"
for b in bash dirname cat jq mktemp sed rm; do ln -s "$(env -i bash -c "command -v $b")" "$MINBIN/$b" 2>/dev/null; done
[ -x "$MINBIN/bash" ] || { echo "  FAIL MINBIN setup (bash not linked)"; fail=$((fail+1)); }
_sb="$(sandbox "$DIRTY")"
run_raw_env() { OUTPUT="$(env CLAUDE_PROJECT_DIR="$1" PATH="$2" "$HOOK" <<<"$3" 2>&1)"; RC=$?; }
run_raw_env "$_sb" "$MINBIN" '{"tool_name":"Bash","tool_input":{"command":"git commit -m x"}}'
ok "commit + no grep → block (was fail-OPEN)"  'blocked_closed'
run_raw_env "$_sb" "$MINBIN" '{"tool_name":"Bash","tool_input":{"command":"ls -la"}}'
ok "non-commit + no grep → allow (no deadlock)" 'allowed'
NOJQ="$(mktemp -d -p "$ROOT")"
for b in bash dirname cat grep mktemp sed rm; do ln -s "$(env -i bash -c "command -v $b")" "$NOJQ/$b" 2>/dev/null; done
run_raw_env "$_sb" "$NOJQ" '{"tool_name":"Bash","tool_input":{"command":"git commit -m x"}}'
ok "commit + no jq → block"                    'blocked_closed'
run_raw_env "$_sb" "$NOJQ" '{"tool_name":"Bash","tool_input":{"command":"ls"}}'
ok "non-commit + no jq → allow (no deadlock)"  'allowed'

echo ""
echo "The override — hook mode only, and never silent:"
_sb="$(sandbox "$DIRTY")"
run_hook_env() { OUTPUT="$(env CLAUDE_PROJECT_DIR="$1" NETPACE_ALLOW_SKIPS=1 "$HOOK" <<<'{"tool_name":"Bash","tool_input":{"command":"git commit -m x"}}' 2>&1)"; RC=$?; }
run_hook_env "$_sb"; ok "NETPACE_ALLOW_SKIPS=1 + dirty tree → allow" 'allowed'
ok "…and announces itself on stderr"           'printf "%s" "$OUTPUT" | grep -q "BYPASSED"'
# An audit must not be silenceable by a stray env var, or a leaked override turns CI into a
# green-reporting no-op (#91 H6).
run_check "$_sb" NETPACE_ALLOW_SKIPS=1;            ok "--check IGNORES the override → still 1" '[ "$RC" = "1" ]'

echo ""
echo "--check mode — distinct exit codes from hook mode (1 = found, not 2):"
run_check "$(sandbox "$CLEAN")";                                    ok "--check clean → 0" 'allowed'
run_check "$(sandbox 'class T { void M(){ Assert.Skip("x"); } }')"; ok "--check finds a skip → 1" '[ "$RC" = "1" ]'
run_check "$(empty_sandbox)";                                       ok "--check no src/ → 2 (fail closed)" 'blocked_closed'

echo ""
echo "Root resolution (#91 H8) — CLAUDE_PROJECT_DIR must win over the script's own location:"
# Every case above depends on this. If the gate ever reverts to a purely location-derived root,
# each sandbox would silently scan the REAL repo instead — green while verifying nothing.
run_hook "$(sandbox "$DIRTY")"; ok "sandbox skip is seen (env root honoured, not \$HERE/../..)" 'blocked_detect'
run_hook "$(sandbox "$CLEAN")"; ok "clean sandbox is clean (real repo's src/ not scanned)"    'allowed'

echo ""
echo "KNOWN OVER-BLOCKS — deliberate, pinned so a future 'tighten this' sees the trade:"
# BANNED_RE carries a bare `Skip…=` to catch [Fact(Skip = GateReason)], where the reason is an
# identifier. That breadth also matches ordinary C# assigning to something named Skip. Over-blocking
# is the safe failure — loud, and fixed by a rename — where a false pass is silent non-coverage,
# which is what §X exists to prevent.
run_hook "$(sandbox 'class T { void M(){ var q = new Page { Skip = 10, Take = 5 }; } }')"
ok "pagination initializer { Skip = 10 }" 'blocked_detect'
run_hook "$(sandbox 'class T { void M(){ int Skip = 0; } }')"
ok "local variable named Skip"            'blocked_detect'
# #91 H7, assessed and accepted: classification is not narrowed to command-word position. A
# read-only command merely MENTIONING the words is gated — but only when src/ already holds a
# banned construct, i.e. when §X is already violated and must be fixed anyway; in a clean tree
# it never fires. Parsing shell to find the command-word would trade this harmless over-block for
# the failure that matters: a parser gap missing a real commit and letting a skip land silently.
run_hook "$(sandbox "$DIRTY")" 'grep -r "git commit" docs/'
ok "read-only grep mentioning 'git commit'" 'blocked_detect'
run_hook "$(sandbox "$CLEAN")" 'grep -r "git commit" docs/'
ok "…but never on a clean tree"             'allowed'

echo ""
echo "----------------------------------------"
echo "passed: $pass   failed: $fail"
[ "$fail" -eq 0 ] || exit 1
