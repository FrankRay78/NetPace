#!/usr/bin/env bash
#
# no-skipped-tests.sh — static gate banning the entire skipped-test family (Constitution Principle X).
#
# A skipped test reports "green" while checking nothing, so it is silent non-coverage.
# This gate blocks any `git commit` while a banned construct exists anywhere under src/.
# Legitimate needs are met without skips: fail loudly on a missing dependency, exclude
# destructive opt-in suites by [Trait("Category", …)], or document a genuinely untestable
# branch with a comment at the site.
#
# Two modes:
#   --check        Scan the tree and exit 1 if any banned construct is found (for CI / manual).
#   (default)      PreToolUse hook: read hook JSON on stdin, block (exit 2) a `git commit`
#                  that would carry a banned construct.
#
# For a commit it is gating, the gate fails CLOSED: if it cannot locate src/, cannot scan, or
# cannot parse its hook payload (e.g. jq missing), it blocks rather than silently allowing — a
# gate that fails open is the same silent non-coverage it exists to prevent. These fail-closed
# guards apply ONLY after the command is classified as a `git commit`; every non-commit Bash call
# is waved straight through first, so a missing dependency never blocks unrelated commands.
#
# Escape hatch (harness-safety rule: override-first, then tighten): set NETPACE_ALLOW_SKIPS=1
# to bypass. It announces itself on every invocation so it can never be silently in effect.
# Delete this block once the gate is trusted to make the ban absolute.
#
# Wired into .claude/settings.json as a PreToolUse(git commit) hook, so the gate arms
# automatically on every `git commit`. Constitution Principle X makes the ban itself binding.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Banned constructs: xUnit runtime skips (Skip.If/IfNot/Always/Unless, Assert.Skip), the
# SkippableFact/Theory family, the static Skip= attribute arg (string OR identifier, e.g.
# [Fact(Skip = GateReason)]), and SkipException.
BANNED_RE='Skip\.(If|IfNot|Always|Unless)|Assert\.Skip|Skippable(Fact|Theory)|Skip[[:space:]]*=|SkipException'

# Scan src/ for banned constructs. Returns 0 with matching lines on stdout, 0 with no
# output when clean, and 2 when the scan itself errored (permission, unreadable tree) so
# the caller can fail closed instead of reading an error as "clean".
scan() {
  local raw status
  raw="$(grep -rnE --include='*.cs' "$BANNED_RE" "$REPO_ROOT/src")"
  status=$?
  # grep: 0 = match, 1 = no match, >=2 = error.
  if [ "$status" -ge 2 ]; then
    return 2
  fi
  [ -z "$raw" ] && return 0
  printf '%s\n' "$raw" | grep -vE '/(bin|obj)/'
  return 0
}

# Override-first escape hatch — announced, never silent.
if [ "${NETPACE_ALLOW_SKIPS:-}" = "1" ]; then
  echo "no-skipped-tests: WARNING — gate BYPASSED via NETPACE_ALLOW_SKIPS=1 (skip ban NOT enforced)." >&2
  exit 0
fi

if [ "${1:-}" = "--check" ]; then
  # Fail closed if we cannot locate the tree to scan (a wrong REPO_ROOT must not read as clean).
  if [ ! -d "$REPO_ROOT/src" ]; then
    echo "no-skipped-tests: cannot locate src/ under '$REPO_ROOT' — failing closed (exit 2)." >&2
    exit 2
  fi
  hits="$(scan)"; rc=$?
  if [ "$rc" -ge 2 ]; then
    echo "no-skipped-tests: scan of '$REPO_ROOT/src' failed — failing closed (exit 2)." >&2
    exit 2
  fi
  if [ -n "$hits" ]; then
    echo "Banned skipped-test constructs found:" >&2
    echo "$hits" >&2
    exit 1
  fi
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

# Cheap, dependency-free pre-filter: if the raw payload cannot even contain a `git commit`, there
# is nothing to gate — return before touching jq. This deliberately over-matches (any payload
# mentioning git…commit, including a non-Bash tool editing such text); the precise jq parse below
# narrows it. It only ever WIDENS what reaches the real check, so no commit can slip past here.
printf '%s' "$input" | grep -qE '\bgit\b.*\bcommit\b' || exit 0

# jq is required to parse the payload precisely; for a would-be commit a missing jq must fail
# closed, not wave it through.
if ! command -v jq >/dev/null 2>&1; then
  echo "BLOCKED: no-skipped-tests requires jq to parse the hook payload, but jq is not installed — failing closed." >&2
  exit 2
fi

tool="$(printf '%s' "$input" | jq -r '.tool_name // empty')" || {
  echo "BLOCKED: no-skipped-tests could not parse the hook payload as JSON — failing closed." >&2
  exit 2
}
cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty')"

if [ "$tool" != "Bash" ] || ! printf '%s' "$cmd" | grep -qE '\bgit\b.*\bcommit\b'; then
  exit 0
fi

# Confirmed: a Bash `git commit`. Fail closed if we cannot locate the tree to scan.
if [ ! -d "$REPO_ROOT/src" ]; then
  echo "BLOCKED: no-skipped-tests cannot locate src/ under '$REPO_ROOT' — failing closed (exit 2)." >&2
  exit 2
fi

hits="$(scan)"; rc=$?
if [ "$rc" -ge 2 ]; then
  echo "BLOCKED: no-skipped-tests could not scan '$REPO_ROOT/src' — failing closed (exit 2)." >&2
  exit 2
fi
if [ -n "$hits" ]; then
  {
    echo "BLOCKED: commit would introduce/retain banned skipped-test constructs."
    echo "Prohibited: the skip family (Skip.If/IfNot/Always/Unless, Assert.Skip, [Fact/Theory(Skip=…)], [SkippableFact/Theory], SkipException) — see constitution Principle X."
    echo "Fix: make the test fail loudly, exclude destructive suites by [Trait(\"Category\", …)], or document it with a comment at the site."
    echo "Offending lines:"
    echo "$hits"
    echo "(Emergency override only: NETPACE_ALLOW_SKIPS=1)"
  } >&2
  exit 2
fi
exit 0
