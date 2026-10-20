#!/usr/bin/env bash
#
# link-check.sh — every reference to a file in this repository resolves to a file that is there.
#
# Written for the change-intent records, which point at each other and are pointed at from the
# constitution, the workflow docs, CLAUDE.md and two GitHub workflows. A record is a point-in-time
# document that is never updated, so pruning one leaves inbound pointers behind in places nobody
# is reading at the time. This check is what makes that cost visible at the moment of the deletion
# instead of months later. It now covers the markdown links of every tracked `*.md`, which lean on
# each other just as heavily and go stale the same way.
#
# TWO CHECKS, because repository references are written in two shapes here:
#
#   1. A markdown link in a tracked `*.md` file — `[text](target)`. Three kinds of target are
#      resolved, every one of them against the files in this branch and none of them fetched:
#        a. a relative or root-relative path, resolved from the linking file's own directory,
#           which is how GitHub renders it;
#        b. a full `https://github.com/FrankRay78/NetPace/{blob,tree,raw}/main/<path>` link, mapped
#           onto `<path>` in the checkout — the file the link points at, read locally;
#        c. an `#anchor`, on either of those or on its own, checked against the headings of the
#           markdown file it lands in.
#   2. A bare `docs/change-intent-records/<name>.md` path in any tracked text file. Records are
#      cited in prose and in YAML comments, neither of which can carry a markdown link, and those
#      citations go stale exactly as links do.
#
# HOW A HEADING IS MATCHED. GitHub derives a heading's anchor by lowercasing the text, dropping
# everything that is not a letter, digit, hyphen or underscore, turning each remaining space into a
# hyphen, and suffixing `-1`, `-2`, … to the second and later occurrences of a repeated heading.
# That is reproduced here over ATX headings (`## Text`) outside code fences, with one limitation: a
# non-ASCII letter is dropped where GitHub keeps it, so a link to a heading carrying one would be
# reported as unreachable. No heading here carries one, and the report is loud rather than silent.
#
# WHAT IS DELIBERATELY NOT A LINK, in check 1 only. A markdown link inside a fenced block or
# inline backticks is being shown to the reader, not offered to be followed: the harness prompts
# spell out link syntax, `.claude/memory/MEMORY.md` carries `- [Title](file.md) — hook` as its own
# format, and a check that read those would report five dangling links that are nothing of the
# kind. So both are stripped before the link scan, at the cost of not checking a link someone
# wrote inside backticks — which was not a working link to begin with. Check 2 strips neither, so
# a bare record path is read wherever it appears. Also skipped:
#   - a `mailto:` target, and any other URL that is not the full-link shape above. The rest
#     of github.com is a web page rather than a path in this branch — `/releases`, an issue page,
#     a wiki page, an action badge — as is a path on another ref or in another repository, and
#     resolving any of those against the checkout would fail on a perfectly good link.
#   - a relative path that climbs above the repository root, which is outside this repository's
#     reach.
#   - an anchor on a target that is not a markdown file: a `#L10-L20` line range on a source file
#     and a fragment on a folder name no heading.
#
# WHAT IS JUDGED is the WORKING TREE, not `HEAD`: the point of a local run is to be used midway
# through a deletion, before anything is committed, and a check that read `HEAD` would call that
# state clean right up until the commit landed. The file LIST still comes from `git ls-files`, so
# an untracked scratch file is never scanned — but target existence is a plain filesystem test, so
# a link resolving to one passes here and fails in CI. In CI the two are the same thing, because
# the checkout holds only what was committed.
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
for tool in git grep sed awk sort tr dirname mktemp; do
  command -v "$tool" >/dev/null 2>&1 \
    || die "'$tool' is not available, so the check cannot run. It is a merge gate, so it fails rather than reporting an unverified pass."
done

git -C "$REPO_ROOT" rev-parse --git-dir >/dev/null 2>&1 \
  || die "'$REPO_ROOT' is not a git repository, so the tracked files cannot be listed."

TMP="$(mktemp -d)" || die "could not create a temporary directory."
trap 'rm -rf "$TMP"' EXIT

# The records folder, as it is written in a citation. Neither line is itself a reference the
# second check reads: `RECORD_REF`'s filename part is a bracket expression, whose `[` is outside
# the class it spells, and `RECORD_DIR` carries no filename at all.
RECORD_DIR="docs/change-intent-records"
RECORD_REF='docs/change-intent-records/[A-Za-z0-9._-]+\.md'

# This repository's canonical web URL, lowercased, with its trailing slash. Hardcoded rather than
# read from `git remote`: the links in these documents name the canonical repository whoever is
# holding the clone, so a fork's own origin is the wrong thing to resolve them against.
MAIN_URL_PREFIX="https://github.com/frankray78/netpace/"

# --- resolve BASE_DIR TARGET ------------------------------------------------------
# Prints the repository-root-relative path a link target names, or returns 1 when it climbs above
# the root. Done in the shell rather than with `realpath`, which resolves against the real
# filesystem and would follow a symlink out of the tree and succeed on a path that GitHub, which
# resolves textually, would render as broken.
resolve_path() {
  local base="$1" target="$2" joined seg out=""
  local -a segs=() parts=()

  case "$target" in
    /*) joined="${target#/}" ;;                      # root-relative, as GitHub reads a leading /
    *)  if [ "$base" = "." ]; then joined="$target"; else joined="$base/$target"; fi ;;
  esac

  # A non-whitespace IFS does not collapse runs of the delimiter, so `a//b` splits to three
  # segments with an empty one in the middle — dropped below alongside `.`, as GitHub drops it.
  IFS='/' read -r -a segs <<< "$joined"
  for seg in "${segs[@]}"; do
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

# --- main_ref_path URL ------------------------------------------------------------
# Prints the repository-root-relative path a full link to a file on this repository's `main`
# branch names, or returns 1 when the URL is not one. The owner and repository name are matched
# whatever their case, as GitHub serves them; the path part keeps the case it was written in,
# because the checkout does. Lowercasing goes through `tr` rather than `${var,,}` so the check
# runs under the bash 3.2 that is still /bin/bash on macOS.
main_ref_path() {
  local url="$1" lower rest path
  lower="$(printf '%s' "$url" | LC_ALL=C tr 'A-Z' 'a-z')"

  case "$lower" in
    "$MAIN_URL_PREFIX"*) ;;
    *) return 1 ;;
  esac
  rest="${url:${#MAIN_URL_PREFIX}}"
  lower="${lower:${#MAIN_URL_PREFIX}}"

  # Only a path on `main` can be answered for by the files in this branch. `blob` and `raw` name a
  # file, `tree` a folder; anything else under this repository's URL is a web page, and any other
  # ref is a different set of files.
  case "$lower" in
    blob/main/*|tree/main/*|raw/main/*) ;;
    *) return 1 ;;
  esac

  path="${rest#*/main/}"
  path="${path%%\?*}"                                # `?plain=1` and friends are not path
  [ -n "$path" ] || return 1
  resolve_path "." "$path"
}

# --- heading_slugs FILE -----------------------------------------------------------
# Prints the GitHub anchor of every ATX heading in a markdown file, in document order. See
# HOW A HEADING IS MATCHED above. Returns non-zero when the file cannot be read to the end.
heading_slugs() {
  awk '
    match($0, /^[[:space:]]*(```+|~~~+)/) {
      marker = substr($0, RSTART, RLENGTH)
      gsub(/[[:space:]]/, "", marker)
      if (fence == "") { fence = marker; next }
      if (substr(marker, 1, 1) == substr(fence, 1, 1) && length(marker) >= length(fence)) fence = ""
      next
    }
    fence != "" { next }
    /^#+[ \t]/ {
      match($0, /^#+/)
      if (RLENGTH > 6) next                          # seven hashes is not a heading
      line = $0
      sub(/^#+[ \t]+/, "", line)
      sub(/[ \t]+#+[ \t]*$/, "", line)               # an optional closing sequence
      sub(/[ \t]+$/, "", line)
      print line
    }
    END { if (fence != "") exit 3 }
  ' "$REPO_ROOT/$1" \
    | LC_ALL=C tr 'A-Z' 'a-z' \
    | LC_ALL=C tr -dc 'a-z0-9 _\n-' \
    | LC_ALL=C tr ' ' '-' \
    | awk '{ if ($0 in n) { print $0 "-" n[$0]; n[$0]++ } else { n[$0] = 1; print } }'
}

# --- the tracked files ------------------------------------------------------------
git -C "$REPO_ROOT" ls-files -z > "$TMP/tracked" \
  || die "could not list the tracked files of '$REPO_ROOT'."

checked=0
scanned=0
: > "$TMP/missing"
: > "$TMP/noheading"

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
  while IFS= read -r ref; do
    [ -n "$ref" ] || continue
    checked=$((checked + 1))
    [ -e "$REPO_ROOT/$ref" ] || printf '%s\t%s\n' "$file" "$ref" >> "$TMP/missing"
  done <<< "$refs"

  # --- check 1: markdown links, in a tracked *.md file ----------------------------
  case "$file" in *.md) ;; *) continue ;; esac

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

    target_path="${target%%#*}"
    anchor="${target#*#}"
    [ "$anchor" = "$target" ] && anchor=""           # no `#` in the target at all

    case "$target_path" in
      '')
        # An anchor-only target names a heading in the linking file itself.
        resolved="$file"
        ;;
      mailto:*) continue ;;
      *://*)
        # Every scheme goes through the mapping, which returns 1 for anything that is not a path
        # on this repository's `main` — so a scheme written in any case is read, and nothing else
        # about the URL has to be matched twice.
        resolved="$(main_ref_path "$target_path")" || continue
        ;;
      *)
        resolved="$(resolve_path "$dir" "$target_path")" || continue
        ;;
    esac

    if [ -n "$target_path" ]; then
      checked=$((checked + 1))
      if [ ! -e "$REPO_ROOT/$resolved" ]; then
        printf '%s\t%s\n' "$file" "$target_path" >> "$TMP/missing"
        continue                                     # the file is the finding, not its heading
      fi
    fi

    # --- the heading half of check 1 ----------------------------------------------
    [ -n "$anchor" ] || continue
    case "$resolved" in *.md) ;; *) continue ;; esac
    [ -f "$REPO_ROOT/$resolved" ] || continue

    heading_slugs "$resolved" > "$TMP/slugs" \
      || die "could not read the headings of '$resolved', which '$file' links into, so no verdict is possible. An unterminated code fence in it is the commonest cause."
    checked=$((checked + 1))
    grep -qxF -- "$anchor" "$TMP/slugs" \
      || printf '%s\t%s\t%s\n' "$file" "#$anchor" "$resolved" >> "$TMP/noheading"
  done <<< "$targets"
done < "$TMP/tracked"

[ "$scanned" -gt 0 ] \
  || die "found no tracked '*.md' files in '$REPO_ROOT'. This repository has many, so an empty result means the discovery or the naming has drifted — which is the one outcome that would otherwise report green having checked nothing."

# --- the verdict ------------------------------------------------------------------
# Deduplicated: a markdown link to a record matches both checks, and one dangling reference should
# be reported once — the anchor is cut before the pair is recorded so that the two checks agree on
# the string, though a record link written relative to the linking file still differs from a bare
# citation and is reported twice. Both verdicts carry the counts, which makes a run self-diagnosing
# after the fact — "2 references checked" alongside a clean verdict reads very differently from "431".
if [ -s "$TMP/missing" ] || [ -s "$TMP/noheading" ]; then
  {
    if [ -s "$TMP/missing" ]; then
      printf 'link-check: FAIL — these references name a file that is not in the repository:\n'
      sort -u "$TMP/missing" | sed $'s/\t/ → /; s/^/  - /'
      printf '\n'
      printf 'Point each one at where the reasoning now lives, or remove it where the surrounding\n'
      printf 'text no longer needs it. A citation of a deleted %s record is the\n' "$RECORD_DIR"
      printf 'commonest cause.\n'
    fi
    if [ -s "$TMP/noheading" ]; then
      [ -s "$TMP/missing" ] && printf '\n'
      printf 'link-check: FAIL — these links name a heading the file they point at does not have:\n'
      sort -u "$TMP/noheading" | sed $'s/\t/ → /; s/\t/ in /; s/^/  - /'
      printf '\n'
      printf 'Retitle the link to the heading as it now reads, or point it at one that is there.\n'
      printf 'GitHub lowercases a heading, drops anything that is not a letter, digit, hyphen or\n'
      printf 'underscore, and turns each remaining space into a hyphen.\n'
    fi
    printf 'Checked %s reference(s) across %s markdown file(s) and every tracked text file.\n' "$checked" "$scanned"
  } >&2
  exit 1
fi

say "OK — $checked reference(s) checked across $scanned markdown file(s) and every tracked text file, none dangling."
