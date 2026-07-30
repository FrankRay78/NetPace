#!/usr/bin/env bash
#
# traceability-gate.tests.sh — standalone synthetic-fixture matrix for traceability-gate.sh.
#
# The gate reads spec.md / test-plan.md / test *.cs off disk and (in hook mode) dispatches on
# hook_event_name, so every branch is provable by building throwaway spec dirs under a
# throwaway CLAUDE_PROJECT_DIR and piping synthetic hook JSON — no dev stack, no real spec
# required. Exits non-zero on any failure. Run after any edit to the gate.
#
#   Usage:  .claude/hooks/traceability-gate.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/traceability-gate.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
export CLAUDE_PROJECT_DIR="$SB"

pass=0; fail=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- got:[$OUTPUT] rc=$RC"; fail=$((fail+1)); fi; }

# --- fixture builders -------------------------------------------------------------
reset_repo() { rm -rf "$SB/specs" "$SB/src"; mkdir -p "$SB/src"; }
# make_spec DIR "Label A" "Label B" ...  — writes specs/DIR/spec.md with **Scenario:** labels.
make_spec() {
  local d="$SB/specs/$1"; shift; mkdir -p "$d"
  { echo "**Tier**: Production"; echo; echo "### User Story 1"; echo "**Acceptance Scenarios**:"; echo
    local i=1; for s in "$@"; do echo "$i. **Scenario: $s**"; echo "   **Given** x, **When** y, **Then** z"; i=$((i+1)); done
  } > "$d/spec.md"
}
# make_plan DIR "Scenario A" ...  — writes test-plan.md with #### Scenario: headers.
make_plan() {
  local d="$SB/specs/$1"; shift; mkdir -p "$d"
  { echo "### User Story 1"; echo; for s in "$@"; do echo "#### Scenario: $s"; echo "AAA ..."; echo; done; } > "$d/test-plan.md"
}
# make_test FILE "Scenario A" ...  — writes src/FILE with // SCENARIO: markers.
make_test() {
  local f="$SB/src/$1"; mkdir -p "$(dirname "$f")"
  { for s in "$@"; do [ "$s" = "$1" ] && continue; echo "    // SCENARIO: $s"; echo "    [Fact] public void T() { Assert.True(cond); }"; done; } > "$f"
}
run_check() { OUTPUT="$("$HOOK" --check "$@" 2>&1)"; RC=$?; }
stop_json() { printf '{"hook_event_name":"Stop","stop_hook_active":%s}' "${1:-false}"; }
run_stop() { OUTPUT="$(printf '%s' "$(stop_json "${1:-false}")" | "$HOOK" 2>/dev/null)"; RC=$?; }
blocked() { printf '%s' "$OUTPUT" | jq -e '.decision=="block"' >/dev/null 2>&1; }

# ---------------------------------------------------------------------------------
echo "no active spec (steady state):"
reset_repo
run_check;      ok "--check, no specs/ → OK exit 0"        '[ "$RC" = 0 ] && printf "%s" "$OUTPUT" | grep -q "nothing to check"'
run_stop false; ok "Stop, no specs/ → allow"                '[ -z "$OUTPUT" ] && [ "$RC" = 0 ]'
mkdir -p "$SB/specs"
run_stop false; ok "Stop, empty specs/ → allow"             '[ -z "$OUTPUT" ]'

echo ""
echo "edge 1 — spec ⟷ test-plan bijection:"
reset_repo
make_spec F1 "Reports download speed" "Retries on timeout"
make_plan F1 "Reports download speed" "Retries on timeout"
run_check;      ok "matching labels, no markers yet → OK (pre-impl)" '[ "$RC" = 0 ]'
run_stop false; ok "matching labels, no markers → allow (pre-impl)"  '[ -z "$OUTPUT" ]'

make_plan F1 "Reports download speed"
run_check;      ok "spec label missing from plan → FAIL exit 1"       '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "no matching test-plan"'
run_stop false; ok "spec label missing from plan → block"            'blocked'

make_spec F1 "Reports download speed"
make_plan F1 "Reports download speed" "Retries on timeout"
run_check;      ok "plan scenario missing from spec → FAIL"           '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "no matching spec.md"'
run_stop false; ok "plan scenario missing from spec → block (Stop)"  'blocked'

echo ""
echo "duplicate scenario name (unique-key rule):"
reset_repo
make_spec F1 "Dup" "Dup" "Other"      # duplicated label; sets still equal, so only dup fires
make_plan F1 "Dup" "Other"
run_check;      ok "duplicate spec.md label → FAIL"                   '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "duplicate .\*\*Scenario"'
make_spec F1 "Dup" "Other"
make_plan F1 "Dup" "Dup" "Other"      # duplicated header
run_check;      ok "duplicate test-plan.md header → FAIL"            '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "duplicate .#### Scenario"'

echo ""
echo "exact-match semantics (case / whitespace):"
reset_repo
make_spec F1 "Reports download speed"
make_plan F1 "adult patron proceeds"     # case drift
run_check;      ok "case drift → FAIL (case-sensitive)"               '[ "$RC" = 1 ]'
make_spec F1 "  Reports download speed  " # leading/trailing space trimmed
make_plan F1 "Reports download speed"
run_check;      ok "leading/trailing space trimmed → OK"              '[ "$RC" = 0 ]'

echo ""
echo "edge 2 — test-plan → code coverage:"
reset_repo
make_spec F1 "Reports download speed" "Retries on timeout"
make_plan F1 "Reports download speed" "Retries on timeout"
make_test Speedtest.Tests/Ingest.cs "Reports download speed"   # only one of two covered
run_check;      ok "partial coverage → FAIL"                          '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "no matching .// SCENARIO"'
run_stop false; ok "partial coverage → block"                        'blocked'
make_test Speedtest.Tests/Ingest.cs "Reports download speed" "Retries on timeout"
run_check;      ok "full coverage + bijection → OK"                   '[ "$RC" = 0 ]'
run_stop false; ok "full coverage + bijection → allow"               '[ -z "$OUTPUT" ]'
# near-miss marker does not count as coverage
make_test Speedtest.Tests/Ingest.cs "Reports download speed" "Retries on  timeout"  # double space
run_check;      ok "near-miss marker (double space) → FAIL"          '[ "$RC" = 1 ]'
# a longer marker must not satisfy a shorter (substring) scenario — coverage is whole-line exact
reset_repo
make_spec F1 "Reports download speed" "Reports download speed in Mbps"
make_plan F1 "Reports download speed" "Reports download speed in Mbps"
make_test Speedtest.Tests/Ingest.cs "Reports download speed in Mbps"
run_check;      ok "substring scenario not covered by longer marker → FAIL" '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -qE "^ +- Reports download speed$"'
# names carrying regex/shell metacharacters are compared literally (not as patterns)
reset_repo
make_spec F1 "Rejects a.b [edge] c+d"
make_plan F1 "Rejects a.b [edge] c+d"
make_test Speedtest.Tests/Ingest.cs "Rejects a.b [edge] c+d"
run_check;      ok "metacharacter name round-trips exact → OK"       '[ "$RC" = 0 ]'

echo ""
echo "generated obj/bin markers excluded:"
# Two-scenario fixture that DISTINGUISHES exclusion from counting: 'Real covered' has a real
# marker (so edge 2 is armed), 'Obj only' has a marker solely under obj/. If obj/ is excluded
# (correct) 'Obj only' is uncovered → FAIL naming it; if obj/ were counted (the old bug) it
# would read covered → OK. Asserting FAIL pins the exclusion.
reset_repo
make_spec F1 "Real covered" "Obj only"
make_plan F1 "Real covered" "Obj only"
make_test Speedtest.Tests/Ingest.cs "Real covered"
make_test Speedtest.Tests/obj/Debug/Ingest.g.cs "Obj only"
run_check;      ok "obj-only marker excluded → its scenario uncovered → FAIL" '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "Obj only"'
make_test Speedtest.Tests/Ingest.cs "Real covered" "Obj only"           # add the real marker
run_check;      ok "real marker added → OK"                          '[ "$RC" = 0 ]'

echo ""
echo "pre-plan / authoring stages fail open:"
reset_repo
make_spec F1 "Reports download speed"                # spec only, no test-plan
run_stop false; ok "spec.md but no test-plan.md → allow"             '[ -z "$OUTPUT" ]'
make_plan F1                                        # test-plan exists but no scenarios yet
run_stop false; ok "test-plan.md with no scenarios → allow"          '[ -z "$OUTPUT" ]'

echo ""
echo "multiple active specs — one broken:"
reset_repo
make_spec Good "A"; make_plan Good "A"; make_test G.cs "A"
make_spec Bad  "B"; make_plan Bad  "C"
run_check;      ok "one good, one broken → FAIL naming the broken dir" '[ "$RC" = 1 ] && printf "%s" "$OUTPUT" | grep -q "specs/Bad"'
run_check "$SB/specs/Good"; ok "--check scoped to good dir → OK"      '[ "$RC" = 0 ]'
# --check on a target that isn't a spec dir must fail open, not error (typo'd argument path).
mkdir -p "$SB/specs/NoSpec"
run_check "$SB/specs/NoSpec";      ok "--check dir lacking spec.md → OK" '[ "$RC" = 0 ]'
run_check "$SB/specs/DoesNotExist"; ok "--check nonexistent dir → OK"    '[ "$RC" = 0 ]'

echo ""
echo "loop guard / override / fail-open:"
reset_repo
make_spec F1 "A"; make_plan F1 "B"                  # broken, would block
run_stop false; ok "broken chain → block"                            'blocked'
run_stop true;  ok "stop_hook_active=true → allow (loop guard)"      '[ -z "$OUTPUT" ]'
OUTPUT="$(printf '%s' "$(stop_json false)" | NETPACE_SKIP_TRACEABILITY_GATE=1 "$HOOK" 2>"$SB/err")"; RC=$?
ok "NETPACE_SKIP_TRACEABILITY_GATE=1 → no-op + warns"                    '[ -z "$OUTPUT" ] && grep -q BYPASSED "$SB/err"'
OUTPUT="$(printf '' | "$HOOK" 2>/dev/null)"; RC=$?
ok "empty stdin → allow (fail open)"                                 '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
OUTPUT="$(printf '{not json' | "$HOOK" 2>/dev/null)"; RC=$?
ok "malformed JSON stdin → allow (fail open)"                        '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'
OUTPUT="$(printf '{"hook_event_name":"PreToolUse"}' | "$HOOK" 2>/dev/null)"; RC=$?
ok "non-Stop event → allow"                                          '[ "$RC" = 0 ] && [ -z "$OUTPUT" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
