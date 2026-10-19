#!/usr/bin/env bash
#
# link-check.sh — every reference to a file in this repository resolves to a file that is there.
#
# Written for the change-intent records, which point at each other and are pointed at from the
# constitution, the workflow docs, CLAUDE.md and two GitHub workflows. A record is a point-in-time
# document that is never updated, so pruning one leaves inbound pointers behind in places nobody
# is reading at the time. This check is what makes that cost visible at the moment of the deletion
# instead of months later.
#
# TWO CHECKS, because repository references are written in two shapes here:
#
#   1. A markdown link in a tracked `*.md` file — `[text](path)` — resolved relative to the
#      linking file's own directory, which is how GitHub renders it. An `#anchor` suffix is cut
#      before resolving; the anchor itself is not checked.
#   2. A bare `docs/change-intent-records/<name>.md` path in any tracked text file. Records are
#      cited in prose and in YAML comments, neither of which can carry a markdown link, and those
#      citations go stale exactly as links do.
#
# WHAT IS DELIBERATELY NOT A LINK. A target inside a fenced block or inline backticks is being
# shown to the reader, not offered to be followed: the harness prompts spell out link syntax,
# `.claude/memory/MEMORY.md` carries `- [Title](file.md) — hook` as its own format, and a check
# that read those would report six dangling links that are nothing of the kind. So both are
# stripped before the scan, at the cost of not checking a link someone wrote inside backticks —
# which was not a working link to begin with. Also skipped: `http`/`https`/`mailto` targets and
# anchor-only `#fragment` targets, neither of which names a file here, and a relative path that
# climbs above the repository root, which is outside this repository's reach.
#
# WHAT IS JUDGED is the WORKING TREE, not `HEAD`: the point of a local run is to be used midway
# through a deletion, before anything is committed, and a check that read `HEAD` would call that
# state clean right up until the commit landed. The file LIST still comes from `git ls-files`, so
# untracked scratch files are neither scanned nor counted as satisfying a link. In CI the two are
# the same thing, because the checkout holds only what was committed.
#
# DESIGN RULE: fail CLOSED, as scripts/traceability-check.sh does and for the same reason — this
# is a merge gate, and a gate that passes without having run gives exactly the false comfort it
# exists to remove. Finding no tracked `*.md` at all is therefore a failure, not a clean pass:
# this repository has well over a hundred, so an empty result means the discovery or the naming
# has drifted. An unterminated code fence is a failure too, for the same reason — every link
# below it would be invisible, which is not the same as there being none.
#
# Not a Claude Code hook — no .claude/settings.json entry. Two ways to run it:
#   In CI       .github/workflows/link-check.yml, on every pull request to main.
#   Locally     bash scripts/link-check.sh
#               Run it after deleting or retargeting anything that is linked to.
#
# CLAUDE_PROJECT_DIR points it at another tree, which is how link-check.tests.sh drives it.
# Verify any edit with that matrix.

set -uo pipefail

REPO_ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

say() { printf 'link-check: %s\n' "$*"; }
die() { printf 'link-check: FAIL — %s\n' "$*" >&2; exit 1; }

# --- fail-closed preconditions ----------------------------------------------------
for tool in git grep sed awk mktemp; do
  command -v "$tool" >/dev/null 2>&1 \
    || die "'$tool' is not available, so the check cannot run. It is a merge gate, so it fails rather than reporting an unverified pass."
done

git -C "$REPO_ROOT" rev-parse --git-dir >/dev/null 2>&1 \
  || die "'$REPO_ROOT' is not a git repository, so the tracked files cannot be listed."

TMP="$(mktemp -d)" || die "could not create a temporary directory."
trap 'rm -rf "$TMP"' EXIT

# The records folder, as it is written in a citation. Assembled from a prefix and the filename
# pattern separately so that this line is not itself a reference the second check would read.
RECORD_DIR="docs/change-intent-records"
RECORD_REF='docs/change-intent-records/[A-Za-z0-9._-]+\.md'

# --- resolve BASE_DIR TARGET ------------------------------------------------------
# Prints the repository-root-relative path a link target names, or returns 1 when it climbs above
# the root. Done in the shell rather than with `realpath`, which resolves against the real
# filesystem and would follow a symlink out of the tree and succeed on a path that GitHub, which
# resolves textually, would render as broken.
resolve_path() {
  local base="$1" target="$2" joined seg rest out=""
  local -a parts=()

  case "$target" in
    /*) joined="${target#/}" ;;                      # root-relative, as GitHub reads a leading /
    *)  if [ "$base" = "." ]; then joined="$target"; else joined="$base/$target"; fi ;;
  esac

  rest="$joined"
  while [ -n "$rest" ]; do
    seg="${rest%%/*}"
    if [ "$seg" = "$rest" ]; then rest=""; else rest="${rest#*/}"; fi
    case "$seg" in
      ''|.) ;;
      ..)
        [ "${#parts[@]}" -gt 0 ] || return 1
        parts=("${parts[@]:0:${#parts[@]}-1}")
        ;;
      *) parts+=("$seg") ;;
    esac
  done

  [ "${#parts[@]}" -gt 0 ] || return 1
  for seg in "${parts[@]}"; do out="${out:+$out/}$seg"; done
  printf '%s' "$out"
}

# --- the tracked files ------------------------------------------------------------
git -C "$REPO_ROOT" ls-files -z > "$TMP/tracked" \
  || die "could not list the tracked files of '$REPO_ROOT'."

checked=0
scanned=0
: > "$TMP/missing"

markdown_seen=0
while IFS= read -r -d '' file; do
  path="$REPO_ROOT/$file"
  # A tracked path that is gone from the working tree is the half-finished deletion this check
  # exists to be run during. There is nothing to scan in it; what points AT it is the finding,
  # and that is raised by whichever file carries the pointer.
  [ -f "$path" ] || continue

  # --- check 2: a bare citation of a record, in any text file ---------------------
  # `-I` drops binary files, which exit 1 here exactly as a text file with no match does.
  refs="$(grep -oIE -- "$RECORD_REF" "$path")"
  case $? in
    0) ;;
    1) refs="" ;;
    *) die "could not scan '$file' for record citations, so the reference set is incomplete and no verdict is possible." ;;
  esac
  if [ -n "$refs" ]; then
    while IFS= read -r ref; do
      [ -n "$ref" ] || continue
      checked=$((checked + 1))
      [ -e "$REPO_ROOT/$ref" ] || printf '%s\t%s\n' "$file" "$ref" >> "$TMP/missing"
    done <<< "$refs"
  fi

  # --- check 1: markdown links, in a tracked *.md file ----------------------------
  case "$file" in *.md) markdown_seen=1 ;; *) continue ;; esac

  # Fenced blocks first, dropped by their own delimiter so that a `~~~` block quoting a ``` line
  # stays one block; then backtick spans, shortest-first, so `a` and `b` on one line is two spans
  # and not everything between them.
  awk '
    match($0, /^[[:space:]]*(```+|~~~+)/) {
      marker = substr($0, RSTART, RLENGTH)
      gsub(/[[:space:]]/, "", marker)
      if (fence == "") { fence = marker; next }
      if (substr(marker, 1, 1) == substr(fence, 1, 1) && length(marker) >= length(fence)) fence = ""
      next
    }
    fence == "" { gsub(/`[^`]*`/, "", $0); print }
    END { if (fence != "") exit 3 }
  ' "$path" > "$TMP/prose"
  case $? in
    0) ;;
    3) die "'$file' has an unterminated code fence, so any link below it cannot be read. Close the fence — an unreadable file is not a link-free one." ;;
    *) die "could not scan '$file' for code fences, so no verdict is possible." ;;
  esac

  # The target is everything after `](` up to the first space or close paren, which takes the path
  # out of `[text](path "Title")` as well as out of `[text](path)`.
  targets="$(grep -oE '\]\([^)[:space:]]+' "$TMP/prose" | sed -E 's/^\]\(//')"
  case $? in
    0|1) ;;
    *) die "could not scan '$file' for markdown links, so no verdict is possible." ;;
  esac

  scanned=$((scanned + 1))
  [ -n "$targets" ] || continue

  dir="$(dirname "$file")"
  while IFS= read -r target; do
    [ -n "$target" ] || continue
    case "$target" in
      \#*|http://*|https://*|mailto:*|*://*) continue ;;
    esac
    target_path="${target%%#*}"
    [ -n "$target_path" ] || continue

    resolved="$(resolve_path "$dir" "$target_path")" || continue
    checked=$((checked + 1))
    [ -e "$REPO_ROOT/$resolved" ] || printf '%s\t%s\n' "$file" "$target" >> "$TMP/missing"
  done <<< "$targets"
done < "$TMP/tracked"

[ "$markdown_seen" = 1 ] \
  || die "found no tracked '*.md' files in '$REPO_ROOT'. This repository has many, so an empty result means the discovery or the naming has drifted — which is the one outcome that would otherwise report green having checked nothing."

# --- the verdict ------------------------------------------------------------------
# Deduplicated: a markdown link to a record matches both checks, and one dangling reference should
# be reported once. Both verdicts carry the counts, which makes a run self-diagnosing after the
# fact — "2 references checked" alongside a clean verdict reads very differently from "431".
if [ -s "$TMP/missing" ]; then
  {
    printf 'link-check: FAIL — these references name a file that is not in the repository:\n'
    sort -u "$TMP/missing" | sed $'s/\t/ → /; s/^/  - /'
    printf '\n'
    printf 'Point each one at where the reasoning now lives, or remove it where the surrounding\n'
    printf 'text no longer needs it. A citation of a deleted %s record is the\n' "$RECORD_DIR"
    printf 'commonest cause.\n'
    printf 'Checked %s reference(s) across %s markdown file(s) and every tracked text file.\n' "$checked" "$scanned"
  } >&2
  exit 1
fi

say "OK — $checked reference(s) checked across $scanned markdown file(s), none dangling."
