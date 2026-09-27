#!/usr/bin/env bash
#
# no-chmod.sh — refuse an executable-bit `chmod` and name the alternative (issue #293).
#
# `Bash(chmod:*)` sits on `permissions.ask`. An agent that wants to run a throwaway script to
# verify its own work reaches for `chmod +x script.sh`, and that call STALLS an interactive run
# until a human answers, or is SILENTLY DENIED in a headless one — so an unattended
# /build → /verify → /raise-pr chain either waits forever or carries on quietly degraded. In
# the #265 A/B comparisons two reviewers blocked ~84 and ~86 minutes on exactly this call.
#
# `bash script.sh` needs no permission and does the same job. This gate turns the gated call
# into a redirection: it refuses with a message that names the alternative, so the agent learns
# what to do instead of losing a capability with nothing in the run to say so.
#
# SCOPE — executable-bit forms only. `chmod +x`, `chmod u=rx`, `chmod 755` and friends are
# refused. Forms that cannot set the executable bit — `chmod 644`, `chmod -x`, `chmod u+w` —
# are waved through and remain governed by the `ask` rule, which stays in `.claude/settings.json`
# as the fail-open backstop for the cases this gate declines to decide.
#
# DESIGN RULE: fail OPEN, like its sibling green-gate.sh. Any missing tool, unparseable input,
# or undecidable mode (`--reference=`, a `g+u` copy form) exits 0 with no objection — announced on
# stderr for a missing tool, so an unenforced gate is never silently in effect. Only the narrow,
# high-confidence executable-bit case is refused — a gate that falsely blocks is worse than no
# gate, and for a harness we edit with itself a false block can lock out the tools that would
# fix it. The one exception is a decision already MADE and then unwritable: dropping that would
# allow a call the gate had just judged refusable, so the emit path alone fails closed.
#
# KNOWN LIMITS — declined, not overlooked. `chmod` must head a command segment, so a call reached
# through an argument of another command is not seen: `find … -exec chmod +x {} \;`, `… | xargs
# chmod +x`, `bash -c "chmod +x f"`. An unbalanced quote or a command substitution likewise merges
# segments and can hide one. Every such case falls through to the `ask` rule. Each is pinned as an
# explicit allow-case in no-chmod.tests.sh so a reader can tell a declined case from an
# unconsidered one.
#
# Escape hatch (harness-safety: override-first, then tighten): NETPACE_ALLOW_CHMOD=1 makes hook
# mode a no-op, announced on stderr so it can never be silently in effect. `--check` ignores it,
# so a manual or CI classification can never be silenced by a stray env var.
#
# Wired into .claude/settings.json as a PreToolUse(Bash) hook WITHOUT an `if` clause, so the
# segment scan below does all the filtering. The clause is simply not relied upon: what an `if`
# matcher does with a chained command (`cd /repo && chmod +x t.sh`) is not something this repo has
# established — `2026-09-04-push-allow-chmod-ask.md` records the PERMISSION matcher scanning a whole
# compound command, which is a different matcher — and the scan costs one `*chmod*` test on the
# calls that do not match. `green-gate.sh` is wired the same way. Both halves of the wiring are
# asserted in no-chmod.tests.sh, since editing either one silently un-gates the rule.
#
# Two modes:
#   --check '<command>'   Classify one command string: exit 1 + the refusal message if it would
#                         be blocked, 0 otherwise (for CI / manual use).
#   (default)             PreToolUse hook: read hook JSON on stdin, emit a `deny` decision.

set -uo pipefail

# The refusal message. One place, so hook mode and --check say the same thing.
REFUSAL='chmod is prohibited for making a script executable: it is a gated call that stalls an interactive run and is silently denied in a headless one, so an unattended chain loses the capability with nothing in the run to say so. Run the script as `bash script.sh` instead — no permission needed, same result. (Non-executable forms such as `chmod 644` are not refused by this gate. To record the executable bit on a committed file, use `git add --chmod=+x <path>`. Emergency override only: NETPACE_ALLOW_CHMOD=1.)'

# Strip the known-benign leading prefixes from one command SEGMENT so what remains begins with
# the real command. Chained `cd …&&` / `export …&&` leaders need no stripping here — the segment
# split below already makes them segments of their own. Regex cannot parse shell, so only these
# safe leaders are ever stripped:
#
#   env-assignments (`FOO=1 chmod …`)     a wrapper that runs its argument unchanged
#   (`sudo`, `command`, `env`, `exec`, `time`, `nohup`, `rtk`, a leading `\` or `/usr/bin/` path)
#   and the shell keywords that head a compound body (`then`, `else`, `do`, `{`, `(`, `!`) — a
#   `for`/`if` body is a segment of its own once the `;` splits it, so `do chmod +x "$f"` is the
#   form that actually reaches here.
#
# Pure bash on purpose: this runs per segment on every Bash call, and the `sed` it replaces used
# a `:a …; ta` label script, which is a GNU extension that silently yields an empty segment (i.e.
# a missed chmod) on BSD/macOS sed.
strip_segment_prefixes() {
  local s="$1"
  SEG=''
  while :; do
    s="${s#"${s%%[![:space:]]*}"}"
    if [[ "$s" =~ ^[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]] ]] \
      || [[ "$s" =~ ^(sudo|command|env|exec|time|nohup|rtk|then|else|do|\{|!)[[:space:]] ]] \
      || [[ "$s" =~ ^(\(|\\) ]]; then
      s="${s#"${BASH_REMATCH[0]}"}"
      continue
    fi
    break
  done
  # `/usr/bin/chmod` is the same call by another spelling. Only stripped when `chmod` is what
  # follows, so an unrelated absolute path is never rewritten.
  [[ "$s" =~ ^(/[^[:space:]]*/)chmod([[:space:]]|$) ]] && s="${s#"${BASH_REMATCH[1]}"}"
  SEG="$s"
}

# True when $1 — the mode operand of a chmod invocation — sets the executable bit.
#
# Numeric: the x bit is worth 1, so an odd digit in any of the last three positions sets it
# (755, 700, 4755, a bare 7). 644 and 1644 do not. Symbolic: a comma-separated clause whose
# operator is `+` or `=` and whose permission letters include x or X. `X` is included because it
# does set the bit on a directory or an already-executable file, and over-refusing here is loud
# and one edit away from fixed. A `-` clause removes the bit, so `-x` and `a-x` are not refused.
# A copy form (`g+u`) names no literal x and is left undecided — fail open to the `ask` rule.
mode_sets_exec_bit() {
  local mode="$1" clause perms
  # Drop shell quoting the command string may carry around the operand (`chmod "+x" f`).
  mode="${mode//\"/}"
  mode="${mode//\'/}"

  if [[ "$mode" =~ ^[0-7]+$ ]]; then
    # Only the last three digits are user/group/other; any leading digit (setuid/setgid/sticky, or
    # a `0` padding an octal literal) never sets an x bit on its own, so they are dropped before
    # the test — 1644 is not refused while 4755 is. Unbounded digits on purpose: `^[0-7]{1,4}$`
    # made `chmod 04755` fall through to the symbolic loop, which found no `+`/`=` and answered
    # "not executable" — a wrong answer, not a fail-open, while plain `0755` was refused.
    [ ${#mode} -gt 3 ] && mode="${mode: -3}"
    [[ "$mode" =~ [1357] ]] && return 0
    return 1
  fi

  local IFS=,
  for clause in $mode; do
    # Requiring a `+` or `=` here is what leaves a `-` clause (`a-x`, `g-w`) unrefused.
    [[ "$clause" =~ ^[ugoa]*[+=](.*)$ ]] || continue
    perms="${BASH_REMATCH[1]}"
    # A `+`/`=` clause may itself be followed by a `-` clause without a comma (`u+x-w`); only
    # the letters up to the next operator belong to this one.
    perms="${perms%%-*}"
    perms="${perms%%+*}"
    perms="${perms%%=*}"
    [[ "$perms" == *x* || "$perms" == *X* ]] && return 0
  done
  return 1
}

# True when $1 — one already-prefix-stripped command segment — is a chmod that sets the
# executable bit. Word-splitting the segment is safe: only token SHAPES are inspected.
segment_is_exec_chmod() {
  local seg="$1" tok mode=''
  [[ "$seg" =~ ^chmod([[:space:]]|$) ]] || return 1

  # shellcheck disable=SC2086
  set -- $seg
  shift
  for tok in "$@"; do
    case "$tok" in
      # The mode is copied from another file, so this gate cannot know it. Fail open.
      --reference=*) return 1 ;;
      --*) continue ;;
      # A short flag cluster. `-x`, `-rwx` and other symbolic modes do not match, because no
      # chmod flag letter is a permission letter — they fall through to the mode branch.
      -[RvcfHLP]*) continue ;;
      *) mode="$tok"; break ;;
    esac
  done

  [ -n "$mode" ] || return 1
  mode_sets_exec_bit "$mode"
}

# Split a command string into command SEGMENTS, one per output line, in a single quote- and
# heredoc-aware pass. Segment separators are `&&`, `||`, `;`, `|`, `&` and a newline — every form
# in which shell chains one command after another.
#
# Two kinds of text must NOT produce a segment boundary, and both were bugs here before:
#
#   A separator inside a quoted span. `git commit -m "cd /x && chmod +x f"` must not split at the
#   quoted `&&`, or the second half looks like a real invocation — the false refusal this gate's
#   first live call hit, on a command whose only chmod sat inside a quoted JSON payload. A NEWLINE
#   inside a quoted span counts: a multi-line commit message written *about* this rule refused
#   itself until this pass started carrying the quote state across lines.
#
#   A heredoc BODY. A body is data, not commands, so a body line beginning with `chmod` is not an
#   invocation — and the commands most likely to carry one are the ones documenting this very rule
#   (`git commit -F - <<'EOF' … EOF`). Body lines are dropped entirely; everything outside the body
#   is kept, so a real chmod before or after the heredoc is still seen.
#
# Why one awk pass rather than the two bash character walks plus a `sed` split it replaces:
#
#   Cost. `out+="$ch"` per character is quadratic, and this gate fires on EVERY Bash call. A 26 KB
#   `gh pr create --body "…"` that merely mentions `chmod` took 12 s to classify — the gate
#   reintroducing the stall it exists to remove. Streaming whole runs between separators is linear:
#   the same command now classifies in ~40 ms.
#
#   Portability. The `sed -E 's/…/\n/g'` split relied on `\n` in the REPLACEMENT, a GNU extension.
#   BSD/macOS sed emits a literal `n`, collapsing `cd /repo && chmod +x t.sh` into one segment
#   headed by `cd` — the chained form this gate was built for, silently allowed, and CI is
#   ubuntu-only so it would never have shown up.
#
# Quote tracking honours a backslash escape outside quotes and inside double quotes, and treats a
# backslash as literal inside single quotes, as the shell does. `$'…'` is recognised so its
# escaped quote does not desync the tracker. NOT modelled: command substitution, and an
# unbalanced quote. Both make the walk hold a quote level open to end of input, which MERGES
# segments — so the failure direction is a silent MISS, never a false refusal, with the `ask` rule
# behind it to keep the miss costing a prompt rather than being a free pass.
split_segments() {
  printf '%s\n' "$1" | awk '
    BEGIN {
      SQ = sprintf("%c", 39); DQ = sprintf("%c", 34)
      delim_re = "^-?[[:space:]]*[" SQ DQ "]?[A-Za-z_][A-Za-z0-9_]*"
      strip_re = "^-?[[:space:]]*[" SQ DQ "]?"
      q = ""; ansi = 0; body = ""; delim = ""
    }
    {
      if (body != "") {
        # Inside a heredoc body: drop the line, and end the body at its terminator (which may be
        # indented when the heredoc was opened with `<<-`).
        t = $0
        sub(/^[[:space:]]+/, "", t)
        if (t == body) body = ""
        next
      }
      n = length($0); start = 1
      for (i = 1; i <= n; i++) {
        c = substr($0, i, 1)
        # An escaped character is never a separator and never a quote. Inside single quotes a
        # backslash is literal, so the escape does not apply there — unless the span is `$'…'`.
        if (c == "\\" && (q != SQ || ansi)) { i++; continue }
        if (q != "") { if (c == q) q = ""; continue }
        if (c == SQ || c == DQ) {
          q = c
          ansi = (c == SQ && i > 1 && substr($0, i - 1, 1) == "$")
          continue
        }
        if (c == "&" || c == "|" || c == ";") {
          printf "%s\n", substr($0, start, i - start)
          start = i + 1
          continue
        }
        # A heredoc opener, but only a real one: a `<` on either side means this is a herestring
        # (`<<<`, which takes no body) or its middle, and a `)` after the word means an arithmetic
        # left-shift (`$((1 << n))`). Misreading either swallowed every following line — including
        # a real chmod on one of them.
        if (c == "<" && substr($0, i + 1, 1) == "<" && substr($0, i + 2, 1) != "<" \
            && (i == 1 || substr($0, i - 1, 1) != "<")) {
          rest = substr($0, i + 2)
          if (match(rest, delim_re) && substr(rest, RSTART + RLENGTH, 1) != ")") {
            delim = substr(rest, RSTART, RLENGTH)
            sub(strip_re, "", delim)
          }
          i++
          continue
        }
      }
      printf "%s", substr($0, start)
      # A newline still inside a quoted span chains nothing — emit a space, not a boundary.
      if (q != "") printf " "; else printf "\n"
      if (delim != "") { body = delim; delim = "" }
    }
    END { printf "\n" }
  '
}

# True when any segment of $1 is an executable-bit chmod. Matching per segment rather than as a
# whole-command substring is what catches the chained forms an agent actually writes (`cd /repo &&
# chmod +x t.sh`, or a chmod on its own line of a multi-line script) while leaving a mere mention
# of the text alone (`echo "chmod +x t.sh"`, a commit message, a grep pattern) — the mention is an
# argument of another command, never the head of a segment.
command_has_exec_chmod() {
  local cmd="$1" seg
  # Nothing to decide unless the word appears at all. This gate fires on EVERY Bash call, so the
  # overwhelmingly common answer is settled here without spawning anything.
  [[ "$cmd" == *chmod* ]] || return 1
  # Flatten line continuations first: a backslash-newline is not a segment boundary, so
  # `chmod \<newline>  +x t.sh` is one command and must be read as one segment. Doing it here also
  # keeps a trailing backslash out of the split, where it would read as an escaped newline.
  cmd="${cmd//\\$'\n'/ }"
  while IFS= read -r seg; do
    # Assigns SEG rather than returning on stdout: a `$(…)` here would fork once per segment, and
    # a long chained command has many.
    strip_segment_prefixes "$seg"
    segment_is_exec_chmod "$SEG" && return 0
  done < <(split_segments "$cmd")
  return 1
}

# --check is deliberately ABOVE the override: a manual or CI classification must never be
# silenceable by an env var.
if [ "${1:-}" = "--check" ]; then
  if [ $# -lt 2 ]; then
    echo "usage: no-chmod.sh --check '<command>'" >&2
    exit 2
  fi
  if command_has_exec_chmod "$2"; then
    echo "REFUSED: $REFUSAL" >&2
    exit 1
  fi
  exit 0
fi

# --- override-first escape hatch — announced, never silent ------------------------
if [ "${NETPACE_ALLOW_CHMOD:-}" = "1" ]; then
  echo "no-chmod: WARNING — gate BYPASSED via NETPACE_ALLOW_CHMOD=1 (executable-bit chmod refusal NOT enforced)." >&2
  exit 0
fi

# --- fail-open preconditions ------------------------------------------------------
# A missing tool is ANNOUNCED on stderr, to the same standard the override above holds itself to:
# an unenforced gate must never be silently in effect. Behaviour is still fail-open — exit 0 — so
# the call proceeds to the `ask` rule; the line only makes the degraded run visible.
for tool in jq awk; do
  command -v "$tool" >/dev/null 2>&1 && continue
  echo "no-chmod: WARNING — $tool not found; executable-bit chmod refusal NOT enforced." >&2
  exit 0
done

INPUT="$(cat 2>/dev/null)" || exit 0
[ -n "$INPUT" ] || exit 0

jget() { printf '%s' "$INPUT" | jq -r "$1" 2>/dev/null; }

# One jq call for the two routing fields rather than one each: this runs on every Bash call, and
# the overwhelmingly common outcome is a non-match. The command itself needs its own call — it can
# contain tabs and newlines, which any delimited encoding would corrupt.
[ "$(jget '"\(.hook_event_name // "")|\(.tool_name // "")"')" = "PreToolUse|Bash" ] || exit 0

CMD="$(jget '.tool_input.command // empty')"
[ -n "$CMD" ] || exit 0

command_has_exec_chmod "$CMD" || exit 0

# The decision is made; losing it now would ALLOW a call already judged refusable, which is not the
# documented fail-open case (undecidable input) but a decision silently dropped. So if the payload
# cannot be written, fall back to the blocking exit code, whose stderr does reach the model.
if ! jq -n --arg r "$REFUSAL" \
  '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'; then
  printf 'no-chmod: %s\n' "$REFUSAL" >&2
  exit 2
fi
exit 0
