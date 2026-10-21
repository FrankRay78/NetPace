#!/usr/bin/env bash
#
# link-check.tests.sh — case matrix for scripts/link-check.sh.
#
# The check reads a repository: which files are tracked, what they say, and what exists on disk.
# None of that can be handed to it on stdin, so every case builds a throwaway git repo, commits
# fixture files into it, and points the real, unmodified check at that sandbox via
# CLAUDE_PROJECT_DIR. No case touches this repo's own files.
#
# TWO THINGS THIS FILE IS CAREFUL ABOUT:
#
#   1. Passing is not one outcome. "nothing dangling" is the only pass, but a check stubbed to
#      `exit 0` on line one would satisfy a bare `rc == 0`. Every passing case therefore asserts
#      that the verdict names a non-zero number of checked references, so a pass that scanned
#      nothing cannot be mistaken for a clean one.
#   2. No fixture writes the literal change-intent-records path. The check's second half scans
#      every tracked text file in a repository for bare references to that folder, and this file
#      is one — a fixture spelling a missing record out in full would make this matrix the
#      dangling reference it is testing the detection of. The path is assembled from $RECDIR
#      instead, which is also why `grep` for it here finds nothing.
#
# Exits non-zero on any failure. Run after any edit to the check.
#
#   Usage:  scripts/link-check.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECK="$HERE/link-check.sh"

# Assembled, never written out. See note 2 above.
RECDIR="docs/change-intent""-records"

# This repository's canonical web URL, as a full link to a file on `main` is written. The check
# matches a lowercased copy of its own constant against it, so writing it here in canonical case is
# what drives the mapping onto the local checkout — the URL is never fetched.
MAINURL="https://github.com/FrankRay78/NetPace"

# One parent-scoped root holds every sandbox, so cleanup cannot be defeated by a subshell.
ROOT="$(mktemp -d)"
cleanup() { chmod -R u+rwX "$ROOT" 2>/dev/null; rm -rf "$ROOT"; }
trap cleanup EXIT

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC out:[$(printf '%s' "$OUTPUT" | head -4 | tr '\n' '/')]"; fail=$((fail+1)); fi; }

# setup_fail — a broken fixture is not a test result. Every setup step is checked through this,
# because with the fixtures missing a sandbox has no files to scan, and every case asserting
# `failed && names "…"` would still pass while verifying nothing.
setup_fail() { echo "  FIXTURE SETUP FAILED (case $cases): $*" >&2; exit 1; }

new_case() {
  cases=$((cases+1))
  SB="$ROOT/case$cases"
  REPO="$SB/repo"
  # Reset the verdict globals. They are what `ok` reads, so a stale value would let a lost or
  # mis-edited `check` line make the next assertion silently re-assert the previous case's result.
  RC=99
  OUTPUT=""
  mkdir -p "$REPO" || setup_fail "mkdir"
  git -C "$REPO" init -q -b main || setup_fail "git init"
  git -C "$REPO" config user.name link-check-tests || setup_fail "git config name"
  git -C "$REPO" config user.email link-check-tests@example.invalid || setup_fail "git config email"
  git -C "$REPO" config commit.gpgsign false || setup_fail "git config gpgsign"
  git -C "$REPO" commit -q --allow-empty -m init || setup_fail "initial commit"
}

# write_file PATH CONTENT — write a file into the sandbox repo WITHOUT tracking it.
write_file() {
  mkdir -p "$REPO/$(dirname "$1")" || setup_fail "mkdir for $1"
  printf '%s\n' "$2" > "$REPO/$1" || setup_fail "write $1"
}

# commit_file PATH CONTENT — write a file into the sandbox repo and commit it.
commit_file() {
  write_file "$1" "$2"
  git -C "$REPO" add -- "$1" || setup_fail "git add $1"
  git -C "$REPO" commit -q -m "add $1" || setup_fail "git commit $1"
}

# commit_binary PATH CONTENT — commit a file carrying a NUL byte, which `grep -I` treats as binary.
commit_binary() {
  mkdir -p "$REPO/$(dirname "$1")" || setup_fail "mkdir for $1"
  printf 'bin\000%s\n' "$2" > "$REPO/$1" || setup_fail "write $1"
  git -C "$REPO" add -- "$1" || setup_fail "git add $1"
  git -C "$REPO" commit -q -m "add $1" || setup_fail "git commit $1"
}

# delete_committed PATH — remove the file from the working tree, leaving it tracked. This is the
# state a local run meets midway through a deletion, before anything is committed.
delete_committed() { rm -f "$REPO/$1" || setup_fail "rm $1"; }

check() {
  OUTPUT="$(env CLAUDE_PROJECT_DIR="$REPO" bash "$CHECK" 2>&1)"; RC=$?
}

passed() { [ "$RC" = 0 ]; }
failed() { [ "$RC" = 1 ]; }
names()  { printf '%s' "$OUTPUT" | grep -qF -- "$1"; }
# Once and only once — a reference the two checks both match must still be reported a single time.
named_once() { [ "$(printf '%s' "$OUTPUT" | grep -cF -- "$1")" = 1 ]; }
# A pass must have checked something. `OK —` opens the clean verdict and nothing else, and the
# count is asserted non-zero, so a check that scanned no files cannot satisfy this.
clean()  { passed && printf '%s' "$OUTPUT" | grep -qE 'OK — [1-9][0-9]* reference'; }

echo "link-check.sh case matrix"

# --- markdown links in tracked *.md files -----------------------------------------
new_case
commit_file "README.md" 'See [the guide](docs/guide.md) for more.'
commit_file "docs/guide.md" '# Guide'
check
ok "a markdown link to an existing file passes" 'clean'

new_case
commit_file "README.md" 'See [the guide](docs/guide.md) for more.'
check
ok "a markdown link to a missing file fails" 'failed'
ok "  …and names the linking file" 'names "README.md"'
ok "  …and names the target" 'names "docs/guide.md"'

new_case
commit_file "docs/conventions/style.md" 'A [worked example](../examples/one.md).'
commit_file "docs/examples/one.md" '# One'
check
ok "a ../ link that resolves passes" 'clean'

new_case
commit_file "docs/conventions/style.md" 'A [worked example](../examples/one.md).'
commit_file "docs/examples/two.md" '# Two'
check
ok "a ../ link that does not resolve fails" 'failed && names "../examples/one.md"'

new_case
commit_file "README.md" 'Jump to [the part](docs/guide.md#the-part).'
commit_file "docs/guide.md" "$(printf '# Guide\n\n## The Part')"
check
ok "an #anchor suffix is not read as part of the file path" 'clean'

new_case
commit_file "README.md" "$(printf '# Guide\n\n## Overview\n\nJump to [a section](#overview) below, or [the site](https://example.invalid/x.md), or [mail](mailto:a@example.invalid).')"
commit_file "docs/guide.md" '[up](../README.md)'
check
ok "an http or mailto target is no repository path, and an anchor-only one names a heading here" 'clean'

new_case
commit_file "README.md" 'The [docs folder](docs) holds it.'
commit_file "docs/guide.md" '# Guide'
check
ok "a link to a directory that exists passes" 'clean'

new_case
commit_file "docs/a/b.md" 'Up to [the guide](/docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "a root-relative /path resolves from the repository root, as GitHub reads it" 'clean'

new_case
commit_file "docs/a/b.md" 'Up to [the guide](/docs/nope.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "a root-relative /path that is missing fails" 'failed && names "/docs/nope.md"'

new_case
commit_file "README.md" 'A [way out](../../outside.md), and [the guide](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "a target climbing above the repository root is skipped, not reported" 'clean'

new_case
commit_file "README.md" 'An ![diagram](docs/img.png) and a [titled link](docs/guide.md "The Guide").'
commit_file "docs/img.png" 'png'
commit_file "docs/guide.md" '# Guide'
check
ok "an image target and a titled link target are both checked" 'clean'

new_case
commit_file "README.md" 'An ![diagram](docs/gone.png) and [the guide](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "a missing image target is reported" 'failed && names "docs/gone.png"'

new_case
commit_file "README.md" 'A [titled link](docs/gone.md "The Guide").'
check
ok "a title after the target is not read as part of the path" 'failed && names "docs/gone.md"'

# --- what is shown rather than followed -------------------------------------------
new_case
commit_file "README.md" 'Write the pointer as `- [Title](file.md) — hook` and nothing else. Also [real](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "a link inside inline backticks is shown, not followed" 'clean'

new_case
commit_file "README.md" "$(printf 'Before. Also [real](docs/guide.md).\n\n```\n[Title](file.md)\n```\n\nAfter.')"
commit_file "docs/guide.md" '# Guide'
check
ok "a link inside a fenced block is shown, not followed" 'clean'

new_case
commit_file "README.md" "$(printf 'Before. Also [real](docs/guide.md).\n\n~~~\nsee ```\n[Title](file.md)\n~~~\n')"
commit_file "docs/guide.md" '# Guide'
check
ok "a fence is closed only by its own delimiter" 'clean'

new_case
commit_file "README.md" "$(printf 'Opens a fence and never closes it.\n\n```\n[Title](file.md)\n')"
check
ok "an unterminated fence fails rather than passing as link-free" 'failed && names "unterminated"'

# The target sorts after the linking file, so the headings are demanded before the scan reaches
# the file itself — which is what makes this the heading-reading failure and not the one above.
new_case
commit_file "README.md" 'Jump to [a part](zz-target.md#the-part).'
commit_file "zz-target.md" "$(printf '# Guide\n\n```\nnever closed\n')"
check
ok "a linked-into file whose headings cannot be read fails closed" 'failed && names "could not read the headings"'
ok "  …and names the file that links into it" 'names "README.md"'

# --- full links to a path on this repository's main branch ------------------------
new_case
commit_file "README.md" "See [the guide]($MAINURL/blob/main/docs/guide.md)."
commit_file "docs/guide.md" '# Guide'
check
ok "a full blob/main link to an existing file passes" 'clean'

new_case
commit_file "README.md" "See [the provider]($MAINURL/blob/main/src/NetPace.Core/Ookla.cs)."
commit_file "src/NetPace.Core/Ookla.cs" '// code'
check
ok "a full blob/main link keeps the path's case" 'clean'

new_case
commit_file "README.md" "See [the guide]($MAINURL/blob/main/docs/gone.md)."
check
ok "a full blob/main link to a missing file fails" 'failed && names "docs/gone.md"'
ok "  …and names the file that carries it" 'names "README.md"'

new_case
commit_file "README.md" "The [console project]($MAINURL/tree/main/src/App)."
check
ok "a full tree/main link to a missing folder fails" 'failed && names "src/App"'

new_case
commit_file "README.md" "A [raw file]($MAINURL/raw/main/docs/gone.md)."
check
ok "a full raw/main link to a missing file fails" 'failed && names "docs/gone.md"'

new_case
commit_file "README.md" "See [the guide](HTTPS://GitHub.com/FrankRay78/NETPACE/BLOB/MAIN/docs/gone.md)."
check
ok "every segment of a full link but the path is matched whatever its case" 'failed && names "docs/gone.md"'

new_case
commit_file "README.md" "See [the guide](HTTPS://GitHub.com/FrankRay78/NETPACE/BLOB/MAIN/docs/guide.md)."
commit_file "docs/guide.md" '# Guide'
check
ok "  …and an uppercase ref still resolves onto the local path" 'clean'

# Over-reach is the risk here: everything else on github.com is a web page, not a path in this
# branch, and a check that resolved those against the checkout would fail on correct links.
new_case
commit_file "README.md" "Releases are [here]($MAINURL/releases), issues [there]($MAINURL/issues/new/choose), the history is [here]($MAINURL/commits/main/docs/gone.md), and the repo is [this]($MAINURL). Also [the guide](docs/guide.md)."
commit_file "docs/guide.md" '# Guide'
check
ok "a GitHub page that is not a path on main is not resolved against the checkout" 'clean'

new_case
commit_file "README.md" "A [tagged file]($MAINURL/blob/v1.2.3/docs/gone.md), and [the guide](docs/guide.md)."
commit_file "docs/guide.md" '# Guide'
check
ok "a full link to a path on another ref is not checked" 'clean'

new_case
commit_file "README.md" 'A [file elsewhere](https://github.com/owner/repo/blob/main/src/Foo.cs), and [the guide](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "a full link into another repository is not checked" 'clean'

# --- links to a heading, in the same file or another one --------------------------
new_case
commit_file "README.md" "$(printf '# Guide\n\n## The Overview\n\nJump to [the overview](#the-overview).')"
check
ok "a same-file anchor naming a heading that exists passes" 'clean'

new_case
commit_file "README.md" "$(printf '# Guide\n\n## The Overview\n\nJump to [nowhere](#the-summary).')"
check
ok "a same-file anchor naming a heading that does not exist fails" 'failed && names "#the-summary"'
ok "  …and names the file that carries it" 'names "README.md"'

new_case
commit_file "README.md" 'Jump to [a part](docs/guide.md#the-part).'
commit_file "docs/guide.md" "$(printf '# Guide\n\n## The Part')"
check
ok "an anchor into another file naming a heading that exists passes" 'clean'

new_case
commit_file "README.md" 'Jump to [a part](docs/guide.md#the-missing-part).'
commit_file "docs/guide.md" "$(printf '# Guide\n\n## The Part')"
check
ok "an anchor into another file naming a heading that does not exist fails" 'failed && names "#the-missing-part"'

new_case
commit_file "README.md" "Jump to [a part]($MAINURL/blob/main/docs/guide.md#the-missing-part)."
commit_file "docs/guide.md" "$(printf '# Guide\n\n## The Part')"
check
ok "an anchor on a full blob/main link is checked against the local file" 'failed && names "#the-missing-part"'

# The linking file deliberately carries no heading of its own, so a check that matched the anchor
# against the WRONG file — the linking one rather than the target — would fail this case.
new_case
commit_file "README.md" "Jump to [a part]($MAINURL/blob/main/docs/guide.md#the-part)."
commit_file "docs/guide.md" "$(printf '# Guide\n\n## The Part')"
check
ok "an anchor on a full blob/main link resolves against the target file's headings" 'clean'

new_case
commit_file "README.md" "Read [the source]($MAINURL/blob/main/docs/guide.md?plain=1), and [again](docs/guide.md)."
commit_file "docs/guide.md" '# Guide'
check
ok "a ?query on a full link is not read as part of the path" 'clean'

# GitHub's own "copy permalink" writes a line reference this way, and it names no heading.
new_case
commit_file "README.md" "Read [line ten]($MAINURL/blob/main/docs/guide.md?plain=1#L10), and [a range](docs/guide.md#L10-L20)."
commit_file "docs/guide.md" '# Guide'
check
ok "an #L line reference on a markdown file is not read as a heading" 'clean'

# GitHub's slug keeps letters, digits, hyphens and underscores, drops everything else, and turns
# each remaining space into a hyphen — so a spaced em dash leaves two.
new_case
commit_file "README.md" "$(printf '# Guide\n\n## Appendix — Codebase Setup\n\n### Running with `--diagnostics` (§3)\n\nSee [the appendix](#appendix--codebase-setup) and [the flag](#running-with---diagnostics-3).')"
check
ok "heading case, punctuation and spacing are slugified as GitHub does" 'clean'

# GitHub slugs the rendered heading, so a link in one contributes its text and not its target.
# Folding the target in would make the anchor below unreachable on a link that works on GitHub.
new_case
commit_file "README.md" "$(printf '# Guide\n\n### 2.1 Download — [`Ookla.GetAsync`](../src/Ookla.cs)\n\nSee [it](#21-download--ooklagetasync).')"
commit_file "src/Ookla.cs" '// code'
check
ok "a link inside a heading slugs as its text, not its target" 'clean'

new_case
commit_file "README.md" "$(printf '# Guide\n\n## The Overview\n\nJump to [it](#The-Overview).')"
check
ok "an anchor in the wrong case does not match GitHub's lowercase slug" 'failed && names "#The-Overview"'

# A repeated heading is disambiguated by GitHub with a counter, the first occurrence unsuffixed.
new_case
commit_file "README.md" "$(printf '# Guide\n\n## The Stage\n\n## The Stage\n\nFirst [one](#the-stage), second [two](#the-stage-1).')"
check
ok "a repeated heading takes GitHub's -1 suffix on its second occurrence" 'clean'

new_case
commit_file "README.md" "$(printf '# Guide\n\n## The Stage\n\n## The Stage\n\nThird [three](#the-stage-2).')"
check
ok "a counter beyond the repeat count does not resolve" 'failed && names "#the-stage-2"'

new_case
commit_file "README.md" "$(printf '# Guide\n\nJump to [it](#treat-this-as-a-failure).\n\n```bash\n# Treat this as a failure\n```\n')"
check
ok "a hash line inside a fenced block is not a heading" 'failed && names "#treat-this-as-a-failure"'

# The target is a shell script with a `# Usage` comment, so only the not-markdown guard keeps the
# anchor from being matched against comment lines: an `L…` anchor would be skipped by the
# line-reference rule first, and would pass whether the guard existed or not.
new_case
commit_file "README.md" 'The [usage](scripts/foo.sh#nowhere), and [the guide](docs/guide.md).'
commit_file "scripts/foo.sh" "$(printf '#!/usr/bin/env bash\n# Usage\n')"
commit_file "docs/guide.md" '# Guide'
check
ok "an anchor on a target that is not markdown is not read as a heading" 'clean'

new_case
commit_file "README.md" "$(printf '# Guide\n\n####### Seven\n\nJump to [it](#seven).')"
check
ok "seven hashes is not a heading" 'failed && names "#seven"'

new_case
commit_file "README.md" "$(printf '# Guide\n\n## Foo ##\n\nJump to [it](#foo).')"
check
ok "a heading's closing hashes are not part of its slug" 'clean'

new_case
commit_file "README.md" "$(printf '# Guide\n\n<!--\n## Old section\n-->\n\nJump to [it](#old-section).')"
check
ok "a hash line inside an HTML comment is not a heading" 'failed && names "#old-section"'

new_case
commit_file "README.md" "$(printf -- '---\ntitle: Guide\n# note\n---\n\n# Guide\n\nJump to [it](#note).')"
check
ok "a hash line inside front matter is not a heading" 'failed && names "#note"'

new_case
commit_file "README.md" "$(printf '# Guide\n\n```markdown\n```bash\n# Run this\n```\n\nJump to [it](#run-this).')"
check
ok "a fence line with an info string does not close an open fence" 'failed && names "#run-this"'

new_case
commit_file "README.md" 'The [docs folder](docs#anything), and [the guide](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
check
ok "an anchor on a folder target is not read as a heading" 'clean'

new_case
commit_file "README.md" 'See [the guide](docs/gone.md#the-part).'
commit_file "docs/guide.md" '# Guide'
check
ok "a missing file with an anchor is reported as the missing file, once" 'failed && named_once "docs/gone.md"'
ok "  …and the heading is not reported as well" '! names "#the-part"'

# --- bare references to the records folder, in any tracked text file ---------------
new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_file ".github/workflows/thing.yml" "# Rationale: $RECDIR/2026-01-01-a-decision.md"
check
ok "a bare record path naming an existing record passes" 'clean'

new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_file ".github/workflows/thing.yml" "# Rationale: $RECDIR/2026-01-02-gone.md"
check
ok "a bare record path naming a missing record fails" 'failed && names "2026-01-02-gone.md"'
ok "  …and names the file that carries it" 'names ".github/workflows/thing.yml"'

new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_file "docs/constitution.md" "Approval: recorded in $RECDIR/2026-01-01-a-decision.md."
check
ok "a trailing sentence period is not read as part of the path" 'clean'

# Check 2 strips neither fences nor backticks — a citation is read wherever it appears. Check 1's
# exemption covers markdown links only, which is what the header and CLAUDE.md say.
new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_file "docs/guide.md" "An example citation reads \`$RECDIR/2026-01-02-gone.md\`."
check
ok "a bare record path inside backticks is still read" 'failed && names "2026-01-02-gone.md"'

new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_binary "data.bin" "$RECDIR/2026-01-02-gone.md"
commit_file "README.md" "The live one is $RECDIR/2026-01-01-a-decision.md."
check
ok "a binary file is not scanned for citations" 'clean'

# Both checks match a dangling record link, and the anchor is cut before the pair is recorded, so
# the two agree on the string and `sort -u` collapses them.
new_case
commit_file "docs/guide.md" "See [the decision]($RECDIR/2026-01-02-gone.md#decisions)."
check
ok "an anchored dangling record link is reported once, not twice" 'failed && named_once "2026-01-02-gone.md"'

new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" "**Supersedes:** [the earlier one]($RECDIR/2026-01-02-gone.md)."
check
ok "a dangling link between records fails" 'failed && names "2026-01-02-gone.md"'

new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '**Supersedes:** [the earlier one](2026-01-02-gone.md).'
check
ok "a dangling folder-relative link between records fails" 'failed && names "2026-01-02-gone.md"'

# The real citation is what keeps the count non-zero, so `clean` cannot be satisfied by a run that
# found nothing at all to check.
new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_file "README.md" "The folder is $RECDIR/ and it holds records, one being $RECDIR/2026-01-01-a-decision.md."
check
ok "the bare folder with no filename is not a reference" 'clean'

# --- the working tree is what is judged -------------------------------------------
new_case
commit_file "$RECDIR/2026-01-01-a-decision.md" '# A decision'
commit_file "docs/guide.md" "See [the decision]($RECDIR/2026-01-01-a-decision.md)."
delete_committed "$RECDIR/2026-01-01-a-decision.md"
check
ok "a record deleted but not yet committed is already dangling" 'failed && names "2026-01-01-a-decision.md"'

new_case
commit_file "README.md" 'See [the guide](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
write_file "notes.md" 'A [dangling](docs/nowhere.md) link in an untracked file.'
check
ok "an untracked file is not scanned" 'clean'

# --- every dangling reference is reported, not just the first ---------------------
new_case
commit_file "README.md" 'One [a](docs/a.md) and two [b](docs/b.md).'
commit_file "docs/guide.md" 'Three [c](c.md).'
check
ok "all three dangling references are named" 'failed && names "docs/a.md" && names "docs/b.md" && names "c.md"'

# --- fail closed ------------------------------------------------------------------
new_case
rm -rf "$REPO/.git" || setup_fail "rm .git"
check
ok "a directory that is not a git repository fails" 'failed && names "git repository"'

new_case
git -C "$REPO" commit -q --allow-empty -m "no files" || setup_fail "empty commit"
check
ok "discovering no markdown files fails rather than passing" 'failed && names "no tracked"'

# The scan itself failing is the case the design rule is written for: a verdict that could not be
# computed must not be reported as a clean one.
new_case
commit_file "README.md" 'See [the guide](docs/guide.md).'
commit_file "docs/guide.md" '# Guide'
chmod 000 "$REPO/docs/guide.md" || setup_fail "chmod guide"
check
ok "a file that cannot be read fails rather than being passed over" 'failed && names "no verdict is possible"'

echo
echo "cases: $cases  assertions passed: $pass  failed: $fail"
[ "$fail" -eq 0 ] || exit 1
