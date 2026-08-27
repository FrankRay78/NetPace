#!/usr/bin/env bash
#
# traceability-gate.sh — deterministic AC↔marker traceability gate.
#
# Constitution §VIII makes the scenario LABEL the traceability key that links
# acceptance criteria → test scenarios → test code:
#
#     spec.md        **Scenario: [name]**     (the acceptance-criteria label)
#        ↓  exact bijection
#     test-plan.md   #### Scenario: [name]    (the planned test scenario)
#        ↓  exact coverage
#     test code      // SCENARIO: [name]       (the implemented test marker)
#
# This gate enforces the two EXACT-MATCH edges of that chain deterministically. The
# JUDGMENT checks (fuzzy name match, mock self-satisfaction, trivially-passing bodies,
# "undocumented test" detection) stay in /speckit.testchecklist — this gate only does
# what a machine can decide without inference. Names match character-for-character after
# trimming leading/trailing whitespace: case, punctuation and internal spacing all count
# (.claude/commands/speckit.testplan.md).
#
#   Edge 1  spec ⟷ test-plan  (bijection): every **Scenario: X** label in spec.md has
#           exactly one matching #### Scenario: X header in test-plan.md, and vice versa.
#           Both are authored docs in the SAME active spec dir, so a mismatch is always a
#           real §VIII violation — there is no false-positive path.
#   Edge 2  test-plan → code  (coverage, DIRECTIONAL): every #### Scenario: X header has
#           at least one matching // SCENARIO: X marker somewhere under src/. The reverse
#           (a marker with no plan scenario) is NOT enforced here — the src/ tree
#           accumulates markers from already-merged features whose specs are deleted, so a
#           global marker→plan bijection would false-block on a clean repo. "Undocumented
#           test" is therefore a judgment call left to /speckit.testchecklist.
#
# SCOPE — active specs only. The gate reads `specs/*/spec.md`. Merged features have their
# specs deleted (they leave only drifted markers behind), so a repo with no active feature —
# the steady state — is a clean no-op. Nothing outside an in-flight spec dir is ever judged.
#
# "No skip markers" is NOT re-implemented here: the whole skip
# family is already gated at commit time by no-skipped-tests.sh (Constitution §X),
# which is strictly stronger (it bans every skip anywhere under src/, traced or not).
# Duplicating it would only create two regexes to drift apart.
#
# Two modes:
#   --check [specdir]   Scan and print a report; exit 1 on any violation, 0 when clean or
#                       when there is no active spec. With a specdir argument, check only
#                       that dir; otherwise every specs/*/ that has a spec.md. For CI / the
#                       human's pre-wire verification.
#   (default)           Stop hook: read hook JSON on stdin and BLOCK ending the turn (once —
#                       loop-guarded via stop_hook_active, exactly like the green completion
#                       gate) when an active spec's chain is broken.
#
# DESIGN RULE: fail OPEN. Missing tool, unparseable input, absent specs/ dir, a spec still
# being authored (no test-plan scenarios yet), or any internal error → exit 0 (no objection).
# The gate acts only on the narrow, high-confidence broken-chain case. As with the other
# harness hooks, a guard that falsely blocks is worse than no guard — doubly so for a harness
# we edit with itself, where a false block can lock out the tools that would fix it.
#
# Escape hatch (harness-safety: override-first, then tighten): NETPACE_SKIP_TRACEABILITY_GATE=1
# makes the whole script a no-op, announced on stderr so it can never be silently in effect.
#
# Wired into .claude/settings.json as the Stop hook. (It once ran alongside the green-gate
# completion gate; that gate was retired in issue #122 when test-green enforcement moved to
# /ship, so this is now the sole Stop hook.) Loop-guarded, so it nudges at most once per turn
# and can never hard-lock. Verify any edit with traceability-gate.tests.sh and --check first;
# see .claude/hooks/README.md.

set -uo pipefail

# --- override-first escape hatch — announced, never silent ------------------------
if [ "${NETPACE_SKIP_TRACEABILITY_GATE:-}" = "1" ]; then
  echo "traceability-gate: WARNING — gate BYPASSED via NETPACE_SKIP_TRACEABILITY_GATE=1 (traceability NOT enforced)." >&2
  exit 0
fi

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

# Trim leading/trailing whitespace only; internal spacing is load-bearing (part of the key).
trim() { sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//'; }

# Extract the scenario NAMES from each artifact, one per line, in file order.
# A name never contains an asterisk (spec) or a newline, so the [^*] / line-based captures
# are exact. grep exits 1 on no match under `set -o pipefail`; `|| true` keeps that benign.
spec_labels()    { grep -oE '\*\*Scenario:[^*]+\*\*' "$1" 2>/dev/null | sed -E 's/^\*\*Scenario:[[:space:]]*//; s/\*\*$//' | trim || true; }
plan_scenarios() { grep -E '^####[[:space:]]+Scenario:' "$1" 2>/dev/null | sed -E 's/^####[[:space:]]+Scenario:[[:space:]]*//' | trim || true; }

# Every // SCENARIO: marker anywhere under src/, generated obj/bin excluded so a built copy
# of a test source can't double-count. The exclusion MUST prune during the directory walk
# (--exclude-dir): -h -o emit only the marker text with no path, so a post-hoc `grep -v obj`
# on that output would silently match nothing — a dead filter (the bug this replaced).
code_markers() {
  [ -d "$ROOT/src" ] || return 0
  grep -rhoE '//[[:space:]]*SCENARIO:.*$' "$ROOT/src" --include='*.cs' \
       --exclude-dir=obj --exclude-dir=bin 2>/dev/null \
    | sed -E 's|^//[[:space:]]*SCENARIO:[[:space:]]*||' | trim || true
}

# Print lines present in $1 but absent from $2 (exact whole-line set difference). Empty
# inputs are handled (comm needs sorted streams; process substitution keeps it in-memory).
minus() { comm -23 <(printf '%s\n' "$1" | sort -u) <(printf '%s\n' "$2" | sort -u) | sed '/^$/d'; }
# Count non-empty lines in a newline list.
count() { printf '%s\n' "$1" | sed '/^$/d' | grep -c '' ; }
# Names appearing more than once in a newline list. §VIII requires the label to be a UNIQUE
# traceability key, so a repeat is a violation even when both sides are set-equal — and the
# set-difference in minus() dedups, so duplicates must be caught explicitly here.
dupes() { printf '%s\n' "$1" | sed '/^$/d' | sort | uniq -d; }

# Check one active spec dir. Echoes a human-readable violation report to stdout and returns
# 1 if the chain is broken, 0 if clean or not-yet-enforceable (pre-plan / pre-implementation).
check_spec() {
  local dir="$1" rel="${1#"$ROOT"/}" spec plan labels scenarios markers
  spec="$dir/spec.md"; plan="$dir/test-plan.md"
  [ -f "$spec" ] || return 0                          # not a spec dir → nothing to judge

  labels="$(spec_labels "$spec")"
  # No test-plan, or a test-plan with no scenarios yet → the contract does not exist yet.
  # Fail open: the spec is still being authored/planned (avoids nagging before the plan lands).
  [ -f "$plan" ] || return 0
  scenarios="$(plan_scenarios "$plan")"
  [ "$(count "$scenarios")" -eq 0 ] && return 0

  local rc=0 out="" miss_plan miss_spec dup_labels dup_scen

  # Edge 1: spec ⟷ test-plan bijection. A repeated name is an ambiguous key, so duplicates on
  # either side are flagged first (minus() below dedups and would miss them).
  dup_labels="$(dupes "$labels")"
  dup_scen="$(dupes "$scenarios")"
  if [ -n "$dup_labels" ]; then
    out+="  duplicate '**Scenario:**' labels in spec.md (each name must be unique — §VIII key):"$'\n'
    out+="$(printf '%s\n' "$dup_labels" | sed 's/^/    - /')"$'\n'; rc=1
  fi
  if [ -n "$dup_scen" ]; then
    out+="  duplicate '#### Scenario:' headers in test-plan.md (each name must be unique — §VIII key):"$'\n'
    out+="$(printf '%s\n' "$dup_scen" | sed 's/^/    - /')"$'\n'; rc=1
  fi
  miss_plan="$(minus "$labels" "$scenarios")"        # spec label with no plan scenario
  miss_spec="$(minus "$scenarios" "$labels")"        # plan scenario with no spec label
  if [ -n "$miss_plan" ]; then
    out+="  spec.md labels with no matching test-plan.md '#### Scenario:' header:"$'\n'
    out+="$(printf '%s\n' "$miss_plan" | sed 's/^/    - /')"$'\n'; rc=1
  fi
  if [ -n "$miss_spec" ]; then
    out+="  test-plan.md scenarios with no matching spec.md '**Scenario:**' label:"$'\n'
    out+="$(printf '%s\n' "$miss_spec" | sed 's/^/    - /')"$'\n'; rc=1
  fi

  # Edge 2: test-plan → code coverage. Enforced only once implementation has STARTED for this
  # spec — i.e. at least one of its scenarios already has a marker. Zero matches → treated as
  # pre-implementation (tests not written) → skip so authoring isn't nagged.
  #
  # The "started" signal is a heuristic over the GLOBAL marker set (markers cannot be scoped to
  # a spec — see the header). Two BOUNDED consequences follow, both accepted:
  #   - false NEGATIVE: if EVERY marker is mistyped (total drift, zero exact matches) the spec
  #     reads as pre-implementation and the gap is missed. Partial drift is still caught (one
  #     correct marker arms the check); /speckit.testchecklist's fuzzy pass backstops total drift.
  #   - false BLOCK: a leftover marker from a merged feature whose name equals one of this spec's
  #     scenarios can arm the check early and nag about the spec's other unwritten scenarios.
  #     Bounded to a single loop-guarded Stop nudge (never a lock-out) plus the override — the
  #     accepted cost of an unscoped, deterministic scan.
  markers="$(code_markers)"
  local covered
  covered="$(comm -12 <(printf '%s\n' "$scenarios" | sort -u) <(printf '%s\n' "$markers" | sort -u) | sed '/^$/d')"
  if [ "$(count "$covered")" -gt 0 ]; then
    local uncovered
    uncovered="$(minus "$scenarios" "$markers")"
    if [ -n "$uncovered" ]; then
      out+="  test-plan.md scenarios with no matching '// SCENARIO:' marker in src/:"$'\n'
      out+="$(printf '%s\n' "$uncovered" | sed 's/^/    - /')"$'\n'; rc=1
    fi
  fi

  if [ "$rc" -ne 0 ]; then
    printf 'Traceability chain broken in %s:\n%s' "$rel" "$out"
  fi
  return "$rc"
}

# Enumerate active spec dirs (those containing spec.md). Steady state: none.
active_specs() {
  local d
  for d in "$ROOT"/specs/*/; do
    [ -f "${d}spec.md" ] && printf '%s\n' "${d%/}"
  done 2>/dev/null
}

# --- --check CLI mode -------------------------------------------------------------
if [ "${1:-}" = "--check" ]; then
  target="${2:-}"
  report=""; violations=0
  if [ -n "$target" ]; then
    dirs="${target%/}"
  else
    dirs="$(active_specs)"
  fi
  if [ -z "$dirs" ]; then
    echo "traceability-gate: no active spec dir (specs/*/spec.md) — nothing to check."
    exit 0
  fi
  while IFS= read -r d; do
    [ -n "$d" ] || continue
    if r="$(check_spec "$d")"; then :; else violations=1; report+="$r"$'\n'; fi
  done <<< "$dirs"
  if [ "$violations" -ne 0 ]; then
    printf '%s' "$report" >&2
    echo "traceability-gate: FAIL — fix the mismatches above (Constitution §VIII)." >&2
    exit 1
  fi
  echo "traceability-gate: OK — every active spec's scenario chain matches."
  exit 0
fi

# --- Stop hook mode ---------------------------------------------------------------
command -v jq >/dev/null 2>&1 || exit 0            # fail open: cannot parse payload
INPUT="$(cat 2>/dev/null)" || exit 0
[ -n "$INPUT" ] || exit 0
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)"
[ "$EVENT" = "Stop" ] || exit 0
# Loop guard: block at most once per turn so the gate can nudge but never hard-lock.
[ "$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false' 2>/dev/null)" = "true" ] && exit 0

report=""; violations=0
while IFS= read -r d; do
  [ -n "$d" ] || continue
  if r="$(check_spec "$d")"; then :; else violations=1; report+="$r"$'\n'; fi
done <<< "$(active_specs)"
[ "$violations" -eq 0 ] && exit 0

reason="$report
Fix the traceability chain before finishing: every spec.md '**Scenario:**' label, its test-plan.md '#### Scenario:' header, and its test '// SCENARIO:' marker must match character-for-character (Constitution §VIII). Run '.claude/hooks/traceability-gate.sh --check' to re-verify, and '/speckit.testchecklist' for the judgment checks. If you are stopping deliberately mid-implementation, say so plainly rather than reporting the chain as complete. (Emergency override only: NETPACE_SKIP_TRACEABILITY_GATE=1)"
jq -n --arg r "$reason" '{decision:"block",reason:$r}'
exit 0
