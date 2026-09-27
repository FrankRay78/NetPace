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
# or undecidable mode (`--reference=`, a `g+u` copy form) exits 0 with no objection. The `ask`
# rule behind it means a fall-through is a prompt, not a free pass. Only the narrow,
# high-confidence executable-bit case is refused — a gate that falsely blocks is worse than no
# gate, and for a harness we edit with itself a false block can lock out the tools that would
# fix it.
#
# Escape hatch (harness-safety: override-first, then tighten): NETPACE_ALLOW_CHMOD=1 makes hook
# mode a no-op, announced on stderr so it can never be silently in effect. `--check` ignores it,
# so a manual or CI classification can never be silenced by a stray env var.
#
# Wired into .claude/settings.json as a PreToolUse(Bash) hook WITHOUT an `if` clause: an `if:
# "Bash(chmod:*)"` would only see a command that starts with `chmod`, and the calls that matter
# most are chained — `cd /repo && chmod +x t.sh`. The segment scan below does the filtering.
#
# Two modes:
#   --check '<command>'   Classify one command string: exit 1 + the refusal message if it would
#                         be blocked, 0 otherwise (for CI / manual use).
#   (default)             PreToolUse hook: read hook JSON on stdin, emit a `deny` decision.

set -uo pipefail

# The refusal message. One place, so hook mode and --check say the same thing.
REFUSAL='chmod is prohibited for making a script executable: it is a gated call that stalls an interactive run and is silently denied in a headless one, so an unattended chain loses the capability with nothing in the run to say so. Run the script as `bash script.sh` instead — no permission needed, same result. (Non-executable forms such as `chmod 644` are not refused by this gate. To record the executable bit on a committed file, use `git add --chmod=+x <path>`. Emergency override only: NETPACE_ALLOW_CHMOD=1.)'

# Strip the known-benign leading prefixes from one command SEGMENT so what remains begins with
# the real command: env-assignments and an optional `rtk` wrapper. Chained `cd …&&` / `export …&&`
# leaders need no stripping here — the segment split below already makes them segments of their
# own. Regex cannot parse shell, so only these safe leaders are ever stripped.
strip_segment_prefixes() {
  printf '%s' "$1" | sed -E '
    s/^[[:space:]]+//
    :a; s/^[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]]+//; ta
    s/^rtk[[:space:]]+//
    s/^[[:space:]]+//
  '
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
  local mode="$1" clause op perms tail
  # Drop shell quoting the command string may carry around the operand (`chmod "+x" f`).
  mode="${mode//\"/}"
  mode="${mode//\'/}"

  # The last up-to-three digits are user/group/other; a leading fourth (setuid/setgid/sticky)
  # never sets an x bit on its own, so 1644 is not refused while 4755 is.
  if [[ "$mode" =~ ^[0-7]{1,4}$ ]] && [[ "$mode" =~ ([0-7]{1,3})$ ]]; then
    tail="${BASH_REMATCH[1]}"
    [[ "$tail" =~ [1357] ]] && return 0
    return 1
  fi

  local IFS=,
  for clause in $mode; do
    [[ "$clause" =~ ^[ugoa]*([+=])(.*)$ ]] || continue
    op="${BASH_REMATCH[1]}"
    perms="${BASH_REMATCH[2]}"
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
      --*)           continue ;;
      # A short flag cluster. `-x`, `-rwx` and other symbolic modes do not match, because no
      # chmod flag letter is a permission letter — they fall through to the mode branch.
      -[RvcfHLP]*)   continue ;;
      *)             mode="$tok"; break ;;
    esac
  done

  [ -n "$mode" ] || return 1
  mode_sets_exec_bit "$mode"
}

# Drop heredoc BODIES before matching. A heredoc body is data, not commands, so a body line
# beginning with `chmod` is not an invocation — and the commands most likely to carry one are the
# ones documenting this very rule (`git commit -F - <<'EOF' … EOF`). Quote masking cannot help
# here: a heredoc body is unquoted text spanning newlines.
#
# Everything outside the body is kept, so a real chmod before or after the heredoc is still seen.
# A herestring (`<<<`) is not a heredoc and is deliberately not matched.
strip_heredoc_bodies() {
  local s="$1" line out='' delim=''
  while IFS= read -r line; do
    if [ -n "$delim" ]; then
      # Inside a body: drop the line, and end the body at its terminator (which may be indented
      # when the heredoc was opened with `<<-`).
      [ "${line#"${line%%[![:space:]]*}"}" = "$delim" ] && delim=''
      continue
    fi
    if [[ "$line" =~ \<\<-?[[:space:]]*[\'\"]?([A-Za-z_][A-Za-z0-9_]*)[\'\"]? ]]; then
      delim="${BASH_REMATCH[1]}"
    fi
    out+="$line"$'\n'
  done <<< "$s"
  printf '%s' "$out"
}

# Neutralise segment separators that sit INSIDE a single- or double-quoted span, so a quoted
# string cannot manufacture a fake segment head. Without this, `git commit -m "cd /x && chmod +x
# f"` splits at the quoted `&&` and the second half looks like a real chmod invocation — which is
# exactly the false refusal the first live call of this gate hit, on a command whose only chmod
# was inside a quoted JSON payload. Quote CHARACTERS are left in place, so a quoted mode
# (`chmod "+x" f`) is still read correctly downstream.
#
# It tracks one quote level and honours a backslash escape inside double quotes. Heredocs,
# `$'…'` and command substitution are not modelled: they leave the gate over-refusing a
# pathological command, which is loud and one edit away from fixed, rather than silently
# missing one.
mask_quoted_separators() {
  local s="$1" out='' i ch q=''
  for (( i = 0; i < ${#s}; i++ )); do
    ch="${s:i:1}"
    if [ -n "$q" ]; then
      if [ "$ch" = '\' ] && [ "$q" = '"' ]; then
        # Escaped character: copy both and skip the separator test for the escapee.
        out+="$ch${s:i+1:1}"
        i=$((i + 1))
        continue
      fi
      if [ "$ch" = "$q" ]; then
        q=''
      else
        case "$ch" in '&' | '|' | ';') ch='_' ;; esac
      fi
    elif [ "$ch" = "'" ] || [ "$ch" = '"' ]; then
      q="$ch"
    fi
    out+="$ch"
  done
  printf '%s' "$out"
}

# True when any segment of $1 is an executable-bit chmod. Segments are separated by `&&`, `||`,
# `;`, `|`, `&` or a newline — every form in which shell chains one command after another.
# Matching per segment rather than as a whole-command substring is what catches the chained forms
# an agent actually writes (`cd /repo && chmod +x t.sh`, or a chmod on its own line of a
# multi-line script) while leaving a mere mention of the text alone (`echo "chmod +x t.sh"`, a
# commit message, a grep pattern) — the mention is an argument of another command, never the head
# of a segment.
command_has_exec_chmod() {
  local cmd="$1" seg
  # Nothing to decide unless the word appears at all. This gate fires on EVERY Bash call, so the
  # overwhelmingly common answer is settled here without a subshell or a character walk.
  [[ "$cmd" == *chmod* ]] || return 1
  # Flatten line continuations first: a backslash-newline is not a segment boundary, so
  # `chmod \<newline>  +x t.sh` is one command and must be read as one segment.
  cmd="${cmd//\\$'\n'/ }"
  [[ "$cmd" == *'<<'* ]] && cmd="$(strip_heredoc_bodies "$cmd")"
  # Only worth walking when the command actually carries quoting.
  if [[ "$cmd" == *\'* || "$cmd" == *\"* ]]; then
    cmd="$(mask_quoted_separators "$cmd")"
  fi
  while IFS= read -r seg; do
    seg="$(strip_segment_prefixes "$seg")"
    segment_is_exec_chmod "$seg" && return 0
  # `printf '%s\n'` (not `%s`): without a trailing newline the final — often only — segment is a
  # partial line that `read` returns non-zero on, so the loop body would never see it.
  done < <(printf '%s\n' "$cmd" | sed -E 's/(\&\&|\|\||[;&|])/\n/g')
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
command -v jq >/dev/null 2>&1 || exit 0
INPUT="$(cat 2>/dev/null)" || exit 0
[ -n "$INPUT" ] || exit 0

jget() { printf '%s' "$INPUT" | jq -r "$1" 2>/dev/null; }

[ "$(jget '.hook_event_name // empty')" = "PreToolUse" ] || exit 0
[ "$(jget '.tool_name // empty')" = "Bash" ] || exit 0

CMD="$(jget '.tool_input.command // empty')"
[ -n "$CMD" ] || exit 0

command_has_exec_chmod "$CMD" || exit 0

jq -n --arg r "$REFUSAL" \
  '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
exit 0
