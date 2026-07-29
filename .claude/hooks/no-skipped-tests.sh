#!/usr/bin/env bash
#
# no-skipped-tests.sh — static gate banning the entire skipped-test family.
#
# A skipped test reports "green" while checking nothing, so it is silent non-coverage.
# This gate blocks any `git commit` while a banned construct exists anywhere under src/.
# Legitimate needs are met without skips: fail loudly on a missing dependency, exclude
# destructive opt-in suites by [Trait("Category", …)], or document a genuinely untestable
# branch with an explanatory comment at the site.
#
# This gate covers the skip FAMILY only (the constructs in BANNED_RE below). The adjacent
# non-coverage patterns it does NOT catch — NotImplementedException test stubs and
# Decision=Pending placeholder traits — are out of scope for this gate.
#
# Two modes:
#   --check        Scan the tree and exit 1 if any banned construct is found (for CI / manual).
#   (default)      PreToolUse hook: read hook JSON on stdin, block (exit 2) a `git commit`
#                  that would carry a banned construct.
#
# For a commit it is gating, the gate fails CLOSED: if it cannot locate src/, cannot scan, or
# cannot parse its hook payload (jq/grep missing), it blocks rather than silently allowing — a
# gate that fails open is the same silent non-coverage it exists to prevent. These fail-closed
# guards apply ONLY after the command is classified as a `git commit`; every non-commit Bash call
# is waved straight through first, so a missing dependency never blocks unrelated commands. The
# classification itself therefore uses shell builtins only — see the pre-filter below, where
# shelling out would reintroduce exactly the fail-open hole these guards exist to close.
#
# Escape hatch (harness-safety rule: override-first, then tighten): set NETPACE_ALLOW_SKIPS=1
# to bypass. It announces itself on every invocation so it can never be silently in effect.
# It applies to HOOK MODE ONLY — `--check` ignores it, so an audit can never be silenced by a
# stray env var. Delete this block once the gate is trusted to make the ban absolute.
#
# Wired into .claude/settings.json as a PreToolUse(Bash) hook, so the gate arms
# automatically on every `git commit`. Constitution §X makes the ban itself binding.

set -uo pipefail

# Root to scan. Honours CLAUDE_PROJECT_DIR (as the harness always sets it, and as the other
# filesystem-reading gates green-gate.sh / traceability-gate.sh already do), falling back to the
# script's own location. Matching traceability-gate.sh here keeps the FS-reading gates consistent
# and lets a test matrix point this one at a sandbox by env var instead of relocating it.
REPO_ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

# Banned constructs: xUnit runtime skips (Skip.If/IfNot/Always/Unless, Assert.Skip), the
# SkippableFact/Theory family, SkipException, and the attribute-argument family — Skip=,
# plus xUnit v3's conditional SkipUnless=/SkipWhen=. The argument alternative deliberately
# matches a string OR an identifier ([Fact(Skip = GateReason)]), which is why it is a bare
# `Skip…=` rather than something anchored to a quote: breadth here over-blocks (loud, fixable
# by a rename) where narrowness would under-block (silent non-coverage — the thing §X exists
# to prevent). Note `If` is a prefix of `IfNot`, so the IfNot alternative never decides a match;
# it is kept for readability of intent.
BANNED_RE='Skip\.(If|IfNot|Always|Unless)|Assert\.Skip|Skippable(Fact|Theory)|Skip(Unless|When)?[[:space:]]*=|SkipException'

# Scan src/ for banned constructs. Returns 0 with matching lines on stdout, 0 with no
# output when clean, and 2 when the scan itself errored (permission, unreadable tree) so
# the caller can fail closed instead of reading an error as "clean". On error, grep's own
# stderr is echoed so the caller can name the offending path rather than reporting a bare
# "could not scan" — one unreadable file blocks every commit, so the diagnostic must point
# at it.
#
# bin/ and obj/ are excluded by grep during traversal (--exclude-dir), NOT by filtering its
# output afterwards. An output filter tests the whole `path:lineno:content` line, so a banned
# construct whose SOURCE TEXT mentions /bin/ or /obj/ would filter itself out and commit clean
# — e.g. `Skip.If(!File.Exists("/bin/bash"), …)`, which is exactly the shape of skip §X most
# often has to stop (a runtime skip guarding a missing dependency, and dependency probes name
# absolute paths).
scan() {
  local raw status
  raw="$(grep -rnE --include='*.cs' --exclude-dir=bin --exclude-dir=obj "$BANNED_RE" "$REPO_ROOT/src" 2>"$SCAN_ERR")"
  status=$?
  # grep: 0 = match, 1 = no match, >=2 = error.
  if [ "$status" -ge 2 ]; then
    return 2
  fi
  [ -z "$raw" ] && return 0
  printf '%s\n' "$raw"
  return 0
}

# Where scan() parks grep's stderr, so an error can be reported with the path that caused it.
SCAN_ERR="$(mktemp 2>/dev/null)" || SCAN_ERR=/dev/null
trap 'rm -f "$SCAN_ERR"' EXIT

# --check is deliberately ABOVE the override: an audit must never be silenceable by an env var.
# NETPACE_ALLOW_SKIPS=1 is a local, per-commit emergency hatch for the interactive agent loop; if it
# leaked into a CI environment it would turn this audit into a no-op that still reports green —
# silent non-coverage dressed as a pass, which is precisely what §X exists to stop. The
# Constitution scopes the override to "genuine emergencies, not routine bypass"; an unattended
# runner is neither.
if [ "${1:-}" = "--check" ]; then
  # Fail closed if we cannot locate the tree to scan (a wrong REPO_ROOT must not read as clean).
  if [ ! -d "$REPO_ROOT/src" ]; then
    echo "no-skipped-tests: cannot locate src/ under '$REPO_ROOT' — failing closed (exit 2)." >&2
    exit 2
  fi
  command -v grep >/dev/null 2>&1 || { echo "no-skipped-tests: grep unavailable — failing closed (exit 2)." >&2; exit 2; }
  hits="$(scan)"; rc=$?
  if [ "$rc" -ge 2 ]; then
    echo "no-skipped-tests: scan of '$REPO_ROOT/src' failed — failing closed (exit 2)." >&2
    [ -s "$SCAN_ERR" ] && sed 's/^/  /' "$SCAN_ERR" >&2
    exit 2
  fi
  if [ -n "$hits" ]; then
    echo "Banned skipped-test constructs found:" >&2
    echo "$hits" >&2
    exit 1
  fi
  exit 0
fi

# Override-first escape hatch — announced, never silent. Hook mode only (see --check above).
if [ "${NETPACE_ALLOW_SKIPS:-}" = "1" ]; then
  echo "no-skipped-tests: WARNING — gate BYPASSED via NETPACE_ALLOW_SKIPS=1 (skip ban NOT enforced)." >&2
  exit 0
fi

# Hook mode: read the PreToolUse JSON on stdin and gate ONLY `git commit`.
#
# Classify the command as a commit BEFORE requiring jq or the src/ tree. This hook fires on
# every Bash call, so a fail-closed dependency check placed ahead of the classification would
# block unrelated commands (`ls`, `cat`, even `apt install jq`) on any box without jq — a
# bootstrap deadlock on a fresh machine. The fail-closed guards below therefore apply only once
# we know this is a commit; a non-commit Bash call is always waved straight through.
input="$(cat)"

# Cheap pre-filter: if the raw payload cannot even contain a `git commit`, there is nothing to
# gate — return before touching any external tool. This deliberately over-matches (any payload
# mentioning git…commit, including a non-Bash tool editing such text); the precise parse below
# narrows it. It only ever WIDENS what reaches the real check, so no commit can slip past here.
#
# It uses BASH PATTERN MATCHING, not grep, and that is load-bearing. This test is the one thing
# that must run before any dependency check — so if it shelled out, a `grep` that was missing or
# erroring would make `… || exit 0` wave a real commit straight through, fail-OPEN, silently.
# `[[ ]]` is a shell builtin: it cannot be absent and cannot fail. (grep is still needed for the
# scan, but only AFTER we know this is a commit — where a hard fail-closed check is safe and does
# not deadlock unrelated commands on a box without it.)
[[ "$input" == *git* && "$input" == *commit* ]] || exit 0

# From here we know the payload mentions git+commit, so it is a would-be commit and every
# uncertainty below must fail CLOSED. jq parses the payload; grep performs the scan. A missing
# either one must block rather than wave through.
if ! command -v jq >/dev/null 2>&1; then
  echo "BLOCKED: no-skipped-tests requires jq to parse the hook payload, but jq is not installed — failing closed." >&2
  exit 2
fi
if ! command -v grep >/dev/null 2>&1; then
  echo "BLOCKED: no-skipped-tests requires grep to scan src/, but grep is not available — failing closed." >&2
  exit 2
fi

tool="$(printf '%s' "$input" | jq -r '.tool_name // empty')" || {
  echo "BLOCKED: no-skipped-tests could not parse the hook payload as JSON — failing closed." >&2
  exit 2
}
cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty')"

# Flatten line continuations before matching. `grep -qE` is line-oriented, so a command split
# across lines — `git \<newline>  commit -m x` — has no single line carrying both tokens, and the
# check below would wave a real commit through. (A bare newline is a genuine command separator
# and is left alone: `git` on one line and `commit` on the next are two unrelated commands, and
# grep -q already matches a `git commit` sitting on any single line of a multi-line script.)
cmd_flat="${cmd//\\$'\n'/ }"

# Deliberately NOT narrowed to command-word position (issue #91 H7). A read-only command that
# merely mentions the words — `grep -r "git commit" docs/` — is gated too, and that over-block is
# accepted: it can only bite when src/ ALREADY holds a banned construct, i.e. when §X is
# already violated and must be fixed regardless; in a clean tree it never fires. Parsing shell to
# find the command-word (as stack-guard.sh does for its own, narrower job) would trade this
# harmless over-block for the failure that actually matters — a parser gap missing a real commit
# and letting a skip land silently.
if [ "$tool" != "Bash" ] || ! printf '%s' "$cmd_flat" | grep -qE '\bgit\b.*\bcommit\b'; then
  exit 0
fi

# Confirmed: a Bash `git commit`. Fail closed if we cannot locate the tree to scan.
if [ ! -d "$REPO_ROOT/src" ]; then
  echo "BLOCKED: no-skipped-tests cannot locate src/ under '$REPO_ROOT' — failing closed (exit 2)." >&2
  exit 2
fi

hits="$(scan)"; rc=$?
if [ "$rc" -ge 2 ]; then
  {
    echo "BLOCKED: no-skipped-tests could not scan '$REPO_ROOT/src' — failing closed (exit 2)."
    # Name the path that failed. One unreadable file blocks EVERY commit, so a bare "could not
    # scan" leaves the operator with a repo-wide lockout and nothing to act on.
    [ -s "$SCAN_ERR" ] && sed 's/^/  /' "$SCAN_ERR"
    echo "Fix the unreadable path above (a stray mode/owner is the usual cause), then retry."
  } >&2
  exit 2
fi
if [ -n "$hits" ]; then
  {
    echo "BLOCKED: commit would introduce/retain banned skipped-test constructs."
    echo "Prohibited: the skip family (Skip.If/IfNot/Always/Unless, Assert.Skip, [Fact/Theory(Skip=…)], [SkippableFact/Theory], SkipException) — see Constitution §X."
    echo "Fix: make the test fail loudly, exclude destructive suites by [Trait(\"Category\", …)], or document the untestable branch with a comment at the site."
    echo "Offending lines:"
    echo "$hits"
    echo "(Emergency override only: NETPACE_ALLOW_SKIPS=1)"
  } >&2
  exit 2
fi
exit 0
