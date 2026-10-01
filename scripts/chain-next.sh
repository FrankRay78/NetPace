#!/usr/bin/env bash
#
# chain-next.sh — build the next ready issue to a pull request, from a timer, unattended.
#
# Each firing picks the lowest-numbered eligible issue — open, labelled `ready`, not `parked`,
# with no open pull request linked to it and no open blocking issue — resets the runner's own
# dedicated clone to the latest main, and runs scripts/chain.sh against it there. It ends with
# either an open pull request (chain.sh's last stage) or the issue labelled `parked` with a
# comment naming the failed stage, the log and the kept branch. It never merges, and it never
# itself deletes or pushes anything on GitHub; only the chain's /raise-pr stage pushes.
#
# WHY. chain.sh carries one named issue to a pull request, but someone still has to pick the
# issue, and return the checkout to a clean main afterwards — the chain leaves its feature branch
# checked out and refuses to start anywhere else. Doing that in the checkout someone also works
# in interactively lets the two collide. This runner owns a clone nobody else touches.
#
# WHAT IT COSTS. A firing with something eligible spends a full chain run of model time and opens
# a real pull request. An empty firing costs a few gh calls and changes nothing.
#
# THE RESET IS DESTRUCTIVE: `git checkout -f`, `reset --hard` and `clean -fdx`. It runs only in a
# clone marked `git config chain-next.dedicated true`, never in a worktree, and only once an issue
# has been selected. Logs and the lock live outside the clone, or `clean -fdx` would delete them.
#
# Prerequisites: git, gh, jq and flock on PATH, plus chain.sh's own (claude, timeout); gh and
# claude signed in; a git identity in the clone. Setup and operation:
# docs/agentic-workflow-NetPace.md (*The chain runner*). Unit files: scripts/systemd/. Tests:
# scripts/chain-next.tests.sh.
#
# Overrides: CHAIN_NEXT_CLONE is the dedicated clone (default ~/Repos/NetPace-runner).
# CHAIN_NEXT_STATE_DIR holds logs/ and the lock (default ${XDG_STATE_HOME:-~/.local/state}/netpace-chain).
#
# The whole body is one function, called on the last line. The reset rewrites this very file
# when main has changed it, and bash reads a script as it runs, so without that the rest of the
# firing would execute whatever bytes now sit at the old offset.

# No -e: every failure below is reported by name rather than killing the firing silently.
set -uo pipefail

usage() {
  echo "usage: scripts/chain-next.sh [--dry-run]" >&2
  echo "  --dry-run: name the issue the next firing would build, and change nothing" >&2
}

say() { echo "chain-next: $*"; }
die() { echo "chain-next: $*" >&2; exit 1; }

# select_issue — print the number of the lowest-numbered eligible issue, or nothing. Fails closed:
# any GitHub query that fails or returns something unreadable ends the firing with an error
# rather than guessing, because a guess in either direction builds the wrong thing or nothing.
select_issue() {
  local issues prs raised candidates n blockers open
  issues=$(gh issue list --state open --label ready --json number,labels --limit 1000) \
    || die "refused — could not list ready issues; nothing selected."
  prs=$(gh pr list --state open --json number,headRefName,closingIssuesReferences --limit 1000) \
    || die "refused — could not list open pull requests; nothing selected."
  # An issue is already raised when an open pull request closes it, or — the fallback for a
  # pull request opened without the closing keyword — when its branch is feature/<N>-….
  raised=$(jq -r '[.[] | (.closingIssuesReferences // [] | .[].number),
      (.headRefName | capture("^feature/(?<n>[0-9]+)-")? | .n | tonumber)] | unique | .[]' <<<"$prs") \
    || die "refused — the open pull request list was unreadable; nothing selected."
  candidates=$(jq -r --argjson raised "$(jq -s . <<<"$raised")" \
      '[.[] | select((.labels | map(.name) | index("parked")) | not) | .number]
       | map(select(. as $n | $raised | index($n) | not)) | sort | .[]' <<<"$issues") \
    || die "refused — the ready issue list was unreadable; nothing selected."
  for n in $candidates; do
    blockers=$(gh api --paginate "repos/{owner}/{repo}/issues/$n/dependencies/blocked_by") \
      || die "refused — could not read what blocks #$n; nothing selected."
    open=$(jq -s '[.[][] | select(.state == "open")] | length' <<<"$blockers") \
      || die "refused — the blocker list for #$n was unreadable; nothing selected."
    if [ "$open" = 0 ]; then echo "$n"; return 0; fi
  done
}

# free_name <prefix> <exists-command…> — <prefix>, or <prefix>-2, -3… — the first the command
# reports as not existing. Several branches set aside in one firing, or two attempts at one issue
# within a second, would otherwise collide.
free_name() {
  local base=$1 name=$1 i=1; shift
  while "$@" "$name"; do i=$((i+1)); name="$base-$i"; done
  echo "$name"
}
branch_exists() { git rev-parse -q --verify "refs/heads/$1" >/dev/null; }
log_exists() { [ -e "$1.log" ]; }

# keep_attempts <n> — rename every local feature/<n>-* branch to attempt/<n>-<stamp>, printing
# the new names. /build stops when the issue's branch already exists with commits it has not
# inspected, so a branch left by a failed or killed run would park the issue again on retry.
# Renaming keeps the work for diagnosis and lets the next /build start clean. Nothing is pushed.
keep_attempts() {
  local n=$1 b kept
  for b in $(git for-each-ref --format='%(refname:short)' "refs/heads/feature/$n-*"); do
    kept=$(free_name "attempt/$n-$STAMP" branch_exists)
    git branch -m "$b" "$kept" || die "could not rename $b to $kept."
    echo "$kept"
  done
}

# reset_clone — the latest main: no local change, no untracked or ignored file, and no local
# feature branch whose upstream is gone (merged and deleted on GitHub). attempt/* branches stay,
# even when a pushed twin they still track has been removed by hand.
reset_clone() {
  local gone
  git fetch -q --prune origin || die "could not fetch origin in $CLONE; nothing was run."
  git checkout -q -f -B main origin/main || die "could not check out origin/main in $CLONE; nothing was run."
  git clean -q -fdx || die "could not clean $CLONE; nothing was run."
  for gone in $(git for-each-ref --format='%(refname:short) %(upstream:track)' refs/heads/feature | awk '$2 == "[gone]" {print $1}'); do
    git branch -q -D "$gone" || say "could not delete $gone, whose upstream is gone; continuing."
  done
}

# park <n> <log> — label the issue parked and say why on it, from the chain's closing line.
park() {
  local n=$1 log=$2 closing stage reason kept remote body
  closing=$(grep -E '^chain: FAILED at \[[0-9]+/5\] ' "$log" | tail -n 1)
  if [ -n "$closing" ]; then
    stage=$(sed -E 's/^chain: FAILED at (\[[0-9]+\/5\] [^ ]+) — .*/\1/' <<<"$closing")
    reason=${closing#* — }
  else
    stage="before the first stage"
    reason=$(grep -v '^[[:space:]]*$' "$log" | tail -n 1)
    reason=${reason:-no output}
  fi
  kept=$(keep_attempts "$n" | paste -sd' ')
  remote=$(git ls-remote --heads origin "refs/heads/feature/$n-*" | awk '{sub("refs/heads/", "", $2); print $2}' | paste -sd' ')
  body="Parked by the chain runner: the chain stopped at **$stage** — $reason"$'\n\n'
  body+="- Full log on the build machine: \`$log\`"$'\n'
  if [ -n "$kept" ]; then
    body+="- The attempt's commits are kept locally in the runner's clone on \`$kept\` (not pushed)."$'\n'
  fi
  if [ -n "$remote" ]; then
    body+="- \`$remote\` was pushed but has no open pull request. The runner never deletes anything on GitHub, so remove or reuse it by hand before un-parking, or the next push will be rejected."$'\n'
  fi
  body+=$'\n'"Remove the \`parked\` label to have the runner build this issue again from scratch."
  gh issue edit "$n" --add-label parked >/dev/null || die "could not label #$n parked — it will be selected again. Log: $log"
  gh issue comment "$n" --body "$body" >/dev/null || die "#$n is parked but the comment could not be posted. Log: $log"
  say "#$n parked — $stage — $reason (log: $log)"
}

main() {
  local dry_run=0 n log rc kept attempts top git_dir common_dir
  if [ "${1:-}" = --dry-run ]; then dry_run=1; shift; fi
  if [ $# -ne 0 ]; then usage; exit 1; fi

  CLONE=${CHAIN_NEXT_CLONE:-$HOME/Repos/NetPace-runner}
  STATE=${CHAIN_NEXT_STATE_DIR:-${XDG_STATE_HOME:-$HOME/.local/state}/netpace-chain}

  for tool in git gh jq flock claude timeout; do
    command -v "$tool" >/dev/null 2>&1 || die "refused — $tool is not on PATH; nothing was run."
  done

  # The reset below is destructive, so the clone must be exactly the one set aside for it: its
  # top level, not a worktree (worktrees share the marker and would share branches with the
  # checkout they belong to), and marked.
  [ -d "$CLONE" ] || die "refused — the runner's clone $CLONE does not exist; see docs/agentic-workflow-NetPace.md (The chain runner)."
  cd "$CLONE" || die "refused — cannot enter $CLONE."
  top=$(git rev-parse --show-toplevel 2>/dev/null) || die "refused — $CLONE is not a git repository."
  git_dir=$(cd "$(git rev-parse --git-dir)" && pwd -P)
  common_dir=$(cd "$(git rev-parse --git-common-dir)" && pwd -P)
  if [ "$(cd "$top" && pwd -P)" != "$(pwd -P)" ] || [ "$git_dir" != "$common_dir" ] \
      || [ "$(git config --get chain-next.dedicated)" != true ]; then
    die "refused — $CLONE is not the runner's dedicated clone (a full clone marked with \`git config chain-next.dedicated true\`); nothing was changed."
  fi

  if [ "$dry_run" = 1 ]; then
    n=$(select_issue) || exit 1
    if [ -n "$n" ]; then say "would build #$n"; else say "nothing eligible — the next firing would do nothing"; fi
    exit 0
  fi

  mkdir -p "$STATE/logs" || die "could not create $STATE/logs."
  # One chain at a time. The timer never starts the service twice, but running the script
  # directly, outside systemd, could overlap it; a firing that finds the lock held is not an
  # error, only a no-op.
  exec 9>>"$STATE/lock" || die "could not open the lock $STATE/lock."
  if ! flock -n 9; then say "a run is already in progress; nothing started."; exit 0; fi

  n=$(select_issue) || exit 1
  if [ -z "$n" ]; then say "nothing eligible."; exit 0; fi

  STAMP=$(date -u +%Y%m%dT%H%M%SZ)
  say "building #$n in $CLONE"
  reset_clone
  attempts=$(keep_attempts "$n") || die "could not set aside an earlier attempt at #$n; nothing was run."
  for kept in $attempts; do say "kept an earlier attempt at #$n as $kept"; done
  log=$(free_name "$STATE/logs/$n-$STAMP" log_exists).log
  # Started from the freshly reset main, so it is the chain.sh main holds now. Its stdin is
  # /dev/null and its descriptor 9 is closed, so no stage can hold the lock after the firing ends.
  bash scripts/chain.sh "$n" </dev/null 9>&- 2>&1 | tee "$log"
  rc=${PIPESTATUS[0]}
  if [ "$rc" = 0 ]; then
    say "#$n raised (log: $log)"
    exit 0
  fi
  park "$n" "$log"
  exit 1
}

main "$@"
