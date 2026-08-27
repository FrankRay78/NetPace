#!/bin/sh
# Claude Code status line — two-line, icon-segmented.
# Adapted from danielmackay/claude-code-statusline (dandoescode.com), fixed for Linux/WSL:
#   - every segment renders only when its field is present (no blank / stray bars)
#   - reset time parses both epoch and ISO-8601 via GNU `date -d`
#   - git runs against the session cwd
#
# Line 1 (session):  🤖 model | 💪 effort | 🧠 context% | 💰 cost | ⏱️ 5h-limit | 📅 7d-limit
# Line 2 (place):    📁 repo | 🌳 worktree | 🌿 branch +added -removed (lines vs HEAD)

input=$(cat)
j() { printf '%s' "$input" | jq -r "$1 // empty" 2>/dev/null; }

model=$(j '.model.display_name'); [ -z "$model" ] && model="Claude"
effort=$(j '.effort.level')
used=$(j '.context_window.used_percentage')
total_cost=$(j '.cost.total_cost_usd')
worktree=$(j '.worktree.name')
current_dir=$(j '.workspace.current_dir')
[ -z "$current_dir" ] && current_dir=$(j '.worktree.original_cwd')
[ -z "$current_dir" ] && current_dir=$(j '.cwd')
[ -z "$current_dir" ] && current_dir="$PWD"
rl5_pct=$(j '.rate_limits.five_hour.used_percentage')
rl5_reset=$(j '.rate_limits.five_hour.resets_at')
rl7_pct=$(j '.rate_limits.seven_day.used_percentage')
rl7_reset=$(j '.rate_limits.seven_day.resets_at')

GREEN='\033[32m'; YELLOW='\033[33m'; RED='\033[31m'
CYAN='\033[36m'; MAGENTA='\033[35m'; DIM='\033[2m'; RESET='\033[0m'
SEP="${DIM}|${RESET}"

# --- git: branch + line insertions/deletions vs HEAD ---
if git -C "$current_dir" rev-parse --git-dir >/dev/null 2>&1; then
  branch=$(git -C "$current_dir" branch --show-current 2>/dev/null)
  [ -z "$branch" ] && branch=$(git -C "$current_dir" rev-parse --abbrev-ref HEAD 2>/dev/null)
  diffstat=$(git -C "$current_dir" diff HEAD --numstat 2>/dev/null | awk '{a+=$1; d+=$2} END {printf "%d %d", a, d}')
  add=${diffstat% *}; del=${diffstat#* }
  git_str="${GREEN}${branch}${RESET}"
  [ "${add:-0}" -gt 0 ] 2>/dev/null && git_str="${git_str} ${GREEN}+${add}${RESET}"
  [ "${del:-0}" -gt 0 ] 2>/dev/null && git_str="${git_str} ${RED}-${del}${RESET}"
else
  git_str="${DIM}no branch${RESET}"
fi

dir_display=$(basename "$(cd "$current_dir" 2>/dev/null && git rev-parse --show-toplevel 2>/dev/null || printf '%s' "$current_dir")")

# --- rate-limit segments (each renders only when its data is present) ---
make_bar() {
  pct=$1; width=10
  filled=$(( pct * width / 100 )); [ "$filled" -gt "$width" ] && filled=$width
  i=0; bar=""
  while [ $i -lt $filled ]; do bar="${bar}█"; i=$((i+1)); done
  while [ $i -lt $width ];  do bar="${bar}░"; i=$((i+1)); done
  printf '%s' "$bar"
}
fmt_reset() {  # $1 = timestamp, $2 = strftime format
  case "$1" in
    '')       : ;;
    *[!0-9]*) date -d "$1"  "$2" 2>/dev/null ;;  # ISO-8601
    *)        date -d "@$1" "$2" 2>/dev/null ;;  # epoch seconds
  esac
}
rl_segment() {  # $1 pct, $2 reset_ts, $3 label, $4 reset_fmt
  [ -z "$1" ] && return
  p=$(printf '%.0f' "$1" 2>/dev/null)
  if   [ "$p" -ge 90 ] 2>/dev/null; then c=$RED
  elif [ "$p" -ge 70 ] 2>/dev/null; then c=$YELLOW
  else c=$GREEN; fi
  rt=$(fmt_reset "$2" "$4")
  seg="${c}$3 $(make_bar "$p") ${p}%"
  [ -n "$rt" ] && seg="${seg} resets ${rt}"
  printf '%s%s' "$seg" "$RESET"
}
rate5=$(rl_segment "$rl5_pct" "$rl5_reset" "5h" '+%-I:%M%p')
rate7=$(rl_segment "$rl7_pct" "$rl7_reset" "7d" '+%a %-I%p')

# --- assemble ---
line1="🤖 ${MAGENTA}${model}${RESET}"
[ -n "$effort" ]     && line1="${line1} ${SEP} 💪 ${effort}"
[ -n "$used" ]       && line1="${line1} ${SEP} 🧠 $(printf '%.0f' "$used" 2>/dev/null)%"
[ -n "$total_cost" ] && line1="${line1} ${SEP} 💰 \$$(awk "BEGIN{printf \"%.2f\", $total_cost}" 2>/dev/null)"
[ -n "$rate5" ]      && line1="${line1} ${SEP} ⏱️ ${rate5}"
[ -n "$rate7" ]      && line1="${line1} ${SEP} 📅 ${rate7}"

line2="📁 ${CYAN}${dir_display}${RESET}"
[ -n "$worktree" ]   && line2="${line2} ${SEP} 🌳 ${worktree}"
line2="${line2} ${SEP} 🌿 ${git_str}"

printf '%b\n%b' "$line1" "$line2"
