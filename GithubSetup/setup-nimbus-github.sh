#!/usr/bin/env bash
#
# setup-nimbus-github.sh
# ----------------------
# Sets up the GitHub planning surface for NickThys3012/Nimbus:
#   - labels (type, epic, priority, area, release-notes automation)
#   - milestones (Sprint 0-6 + Backlog) with due dates
#   - a Projects v2 board with Sprint / Epic / Estimate / Priority fields
#   - 14 epic issues + 70 user stories / tasks, all placed on the board
#
# Requirements: gh (>= 2.40), jq, bash 3.2+ (macOS default is fine)
# Auth:         gh auth login  &&  gh auth refresh -s project,read:project
#
# Usage:
#   ./setup-nimbus-github.sh --dry-run          # show what would happen
#   ./setup-nimbus-github.sh                    # do it
#   ./setup-nimbus-github.sh --start 2026-08-03 # set Sprint 0 start date
#
# The script is idempotent: re-running it will not duplicate labels,
# milestones, the project, or issues (matched on exact title).

set -euo pipefail

# ---------------------------------------------------------------- config ----
REPO="${NIMBUS_REPO:-NickThys3012/Nimbus}"
OWNER="${REPO%%/*}"
DATA_FILE="${NIMBUS_DATA:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/nimbus-backlog.json}"
STATE_FILE="${NIMBUS_STATE:-.nimbus-setup-state.json}"
SPRINT_LENGTH_DAYS=14
START_DATE=""
DRY_RUN=0

while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1; shift ;;
    --start)   START_DATE="$2"; shift 2 ;;
    --repo)    REPO="$2"; OWNER="${REPO%%/*}"; shift 2 ;;
    --data)    DATA_FILE="$2"; shift 2 ;;
    -h|--help) sed -n '2,25p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

# ---------------------------------------------------------------- output ----
c_reset=$'\033[0m'; c_bold=$'\033[1m'; c_dim=$'\033[2m'
c_green=$'\033[32m'; c_yellow=$'\033[33m'; c_red=$'\033[31m'; c_blue=$'\033[34m'

step()  { printf '\n%s==> %s%s\n' "$c_bold$c_blue" "$*" "$c_reset"; }
ok()    { printf '  %s✓%s %s\n' "$c_green" "$c_reset" "$*"; }
skip()  { printf '  %s·%s %s %s(exists)%s\n' "$c_dim" "$c_reset" "$*" "$c_dim" "$c_reset"; }
warn()  { printf '  %s!%s %s\n' "$c_yellow" "$c_reset" "$*"; }
die()   { printf '\n%serror:%s %s\n' "$c_red" "$c_reset" "$*" >&2; exit 1; }

# ------------------------------------------------------------- preflight ----
step "Preflight"

command -v gh >/dev/null 2>&1 || die "GitHub CLI (gh) not found. Install it: https://cli.github.com"
command -v jq >/dev/null 2>&1 || die "jq not found. Install it: https://jqlang.github.io/jq/"
[ -f "$DATA_FILE" ] || die "Backlog data file not found: $DATA_FILE"
jq empty "$DATA_FILE" 2>/dev/null || die "Backlog data file is not valid JSON: $DATA_FILE"

gh auth status >/dev/null 2>&1 || die "Not authenticated. Run: gh auth login"
ok "gh $(gh --version | head -1 | awk '{print $3}') authenticated"

gh repo view "$REPO" >/dev/null 2>&1 || die "Cannot access repo $REPO"
ok "repo $REPO reachable"

SCOPES="$(gh api -i user 2>/dev/null | tr -d '\r' | awk -F': ' 'tolower($1)=="x-oauth-scopes"{print $2}' || true)"
if [ -n "$SCOPES" ] && ! printf '%s' "$SCOPES" | grep -q "project"; then
  warn "token is missing the 'project' scope — the board steps will fail."
  warn "fix with:  gh auth refresh -s project,read:project"
fi

# Cross-platform date arithmetic (GNU date vs BSD/macOS date).
date_add_days() { # $1 = YYYY-MM-DD, $2 = days
  if date -v +1d >/dev/null 2>&1; then
    date -j -v "+${2}d" -f "%Y-%m-%d" "$1" "+%Y-%m-%d"
  else
    date -d "$1 + $2 days" "+%Y-%m-%d"
  fi
}
today() { date "+%Y-%m-%d"; }

if [ -z "$START_DATE" ]; then START_DATE="$(today)"; fi
echo "$START_DATE" | grep -Eq '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' || die "--start must be YYYY-MM-DD"
ok "Sprint 0 starts $START_DATE, ${SPRINT_LENGTH_DAYS}-day sprints"

[ "$DRY_RUN" -eq 1 ] && warn "DRY RUN — nothing will be created"

# State file maps backlog key -> issue number, so re-runs are cheap.
if [ ! -f "$STATE_FILE" ]; then echo '{}' > "$STATE_FILE"; fi
state_get() { jq -r --arg k "$1" '.[$k] // empty' "$STATE_FILE"; }
state_set() {
  [ "$DRY_RUN" -eq 1 ] && return 0
  local tmp; tmp="$(mktemp)"
  jq --arg k "$1" --arg v "$2" '.[$k] = $v' "$STATE_FILE" > "$tmp" && mv "$tmp" "$STATE_FILE"
}

# ---------------------------------------------------------------- labels ----
step "Labels"

while IFS=$'\t' read -r name color desc; do
  [ -z "$name" ] && continue
  if [ "$DRY_RUN" -eq 1 ]; then
    printf '  %s[dry-run]%s label %s\n' "$c_dim" "$c_reset" "$name"
  else
    gh label create "$name" --repo "$REPO" --color "$color" --description "$desc" --force >/dev/null
    ok "$name"
  fi
done < <(jq -r '.labels[] | [.name, .color, .description] | @tsv' "$DATA_FILE")

# ------------------------------------------------------------ milestones ----
step "Milestones"

EXISTING_MS="$(gh api --paginate "repos/$REPO/milestones?state=all" --jq '.[] | "\(.title)\t\(.number)"' 2>/dev/null || true)"

milestone_number_for() { # $1 = title
  printf '%s\n' "$EXISTING_MS" | awk -F'\t' -v t="$1" '$1==t{print $2; exit}'
}

while IFS=$'\t' read -r key title description sprint_index; do
  [ -z "$key" ] && continue
  num="$(milestone_number_for "$title")"
  if [ -n "$num" ]; then
    skip "$title -> #$num"
  elif [ "$DRY_RUN" -eq 1 ]; then
    printf '  %s[dry-run]%s milestone %s\n' "$c_dim" "$c_reset" "$title"
  else
    if [ "$sprint_index" != "null" ] && [ -n "$sprint_index" ]; then
      due="$(date_add_days "$START_DATE" $(( (sprint_index + 1) * SPRINT_LENGTH_DAYS - 1 )))"
      num="$(gh api "repos/$REPO/milestones" -X POST \
              -f title="$title" -f description="$description" \
              -f due_on="${due}T17:00:00Z" --jq '.number')"
      ok "$title -> #$num (due $due)"
    else
      num="$(gh api "repos/$REPO/milestones" -X POST \
              -f title="$title" -f description="$description" --jq '.number')"
      ok "$title -> #$num (no due date)"
    fi
    EXISTING_MS="$EXISTING_MS
$title	$num"
  fi
done < <(jq -r '.milestones[] | [.key, .title, .description, (.sprint_index|tostring)] | @tsv' "$DATA_FILE")

milestone_title_for_key() { jq -r --arg k "$1" '.milestones[] | select(.key==$k) | .title' "$DATA_FILE"; }
sprint_option_for_key()   { jq -r --arg k "$1" '.milestones[] | select(.key==$k) | (if .sprint_index == null then "Backlog" else "Sprint \(.sprint_index)" end)' "$DATA_FILE"; }

# --------------------------------------------------------------- project ----
step "Project board"

PROJECT_TITLE="$(jq -r '.project.title' "$DATA_FILE")"
PROJECT_NUMBER="$(gh project list --owner "$OWNER" --format json --limit 200 2>/dev/null \
  | jq -r --arg t "$PROJECT_TITLE" '.projects[] | select(.title==$t) | .number' | head -1 || true)"

if [ -n "$PROJECT_NUMBER" ]; then
  skip "$PROJECT_TITLE -> project #$PROJECT_NUMBER"
elif [ "$DRY_RUN" -eq 1 ]; then
  printf '  %s[dry-run]%s create project "%s"\n' "$c_dim" "$c_reset" "$PROJECT_TITLE"
  PROJECT_NUMBER="DRYRUN"
else
  PROJECT_NUMBER="$(gh project create --owner "$OWNER" --title "$PROJECT_TITLE" --format json | jq -r '.number')"
  ok "created project #$PROJECT_NUMBER"
fi

if [ "$DRY_RUN" -eq 0 ]; then
  PROJECT_ID="$(gh project view "$PROJECT_NUMBER" --owner "$OWNER" --format json | jq -r '.id')"
  gh project edit "$PROJECT_NUMBER" --owner "$OWNER" \
    --readme "$(jq -r '.project.description' "$DATA_FILE")" >/dev/null 2>&1 || true
else
  PROJECT_ID="DRYRUN"
fi

# --- custom fields ---
field_exists() { # $1 = field name
  [ "$DRY_RUN" -eq 1 ] && return 1
  gh project field-list "$PROJECT_NUMBER" --owner "$OWNER" --format json --limit 100 \
    | jq -e --arg n "$1" '.fields[] | select(.name==$n)' >/dev/null 2>&1
}

create_single_select() { # $1 = name, $2 = comma-separated options
  if field_exists "$1"; then skip "field $1"; return 0; fi
  if [ "$DRY_RUN" -eq 1 ]; then
    printf '  %s[dry-run]%s field %s (%s)\n' "$c_dim" "$c_reset" "$1" "$2"; return 0
  fi
  gh project field-create "$PROJECT_NUMBER" --owner "$OWNER" \
    --name "$1" --data-type SINGLE_SELECT --single-select-options "$2" >/dev/null
  ok "field $1"
}

create_number_field() { # $1 = name
  if field_exists "$1"; then skip "field $1"; return 0; fi
  if [ "$DRY_RUN" -eq 1 ]; then printf '  %s[dry-run]%s field %s (number)\n' "$c_dim" "$c_reset" "$1"; return 0; fi
  gh project field-create "$PROJECT_NUMBER" --owner "$OWNER" --name "$1" --data-type NUMBER >/dev/null
  ok "field $1"
}

SPRINT_OPTS="$(jq -r '.sprint_options | join(",")' "$DATA_FILE")"
EPIC_OPTS="$(jq -r '[.epics[].short] | join(",")' "$DATA_FILE")"

create_single_select "Sprint"   "$SPRINT_OPTS"
create_single_select "Epic"     "$EPIC_OPTS"
create_single_select "Priority" "P0,P1,P2"
create_number_field  "Estimate"

# Cache field metadata once.
if [ "$DRY_RUN" -eq 0 ]; then
  FIELDS_JSON="$(gh project field-list "$PROJECT_NUMBER" --owner "$OWNER" --format json --limit 100)"
  field_id()  { printf '%s' "$FIELDS_JSON" | jq -r --arg n "$1" '.fields[] | select(.name==$n) | .id'; }
  option_id() { printf '%s' "$FIELDS_JSON" | jq -r --arg n "$1" --arg o "$2" \
                  '.fields[] | select(.name==$n) | .options[]? | select(.name==$o) | .id'; }
  F_SPRINT="$(field_id Sprint)"; F_EPIC="$(field_id Epic)"
  F_PRIORITY="$(field_id Priority)"; F_ESTIMATE="$(field_id Estimate)"
fi

# ---------------------------------------------------------------- issues ----
step "Issues"

# Pre-load existing issue titles so re-runs don't duplicate.
EXISTING_ISSUES="$(gh issue list --repo "$REPO" --state all --limit 500 --json number,title \
  --jq '.[] | "\(.title)\t\(.number)"' 2>/dev/null || true)"

issue_number_for_title() {
  printf '%s\n' "$EXISTING_ISSUES" | awk -F'\t' -v t="$1" '$1==t{print $2; exit}'
}

create_issue() { # $1 title, $2 body, $3 milestone title, $4.. labels ; echoes issue number
  local title="$1" body="$2" ms="$3"; shift 3
  local existing; existing="$(issue_number_for_title "$title")"
  if [ -n "$existing" ]; then printf '%s' "$existing"; return 0; fi
  if [ "$DRY_RUN" -eq 1 ]; then printf 'DRY'; return 0; fi

  local args=(issue create --repo "$REPO" --title "$title" --body "$body")
  [ -n "$ms" ] && args+=(--milestone "$ms")
  local l
  for l in "$@"; do args+=(--label "$l"); done

  local url; url="$(gh "${args[@]}")"
  local num="${url##*/}"
  EXISTING_ISSUES="$EXISTING_ISSUES
$title	$num"
  printf '%s' "$num"
}

# --- stories and tasks -------------------------------------------------------
STORY_COUNT="$(jq '.issues | length' "$DATA_FILE")"
echo "  creating $STORY_COUNT stories/tasks..."

i=0
while [ "$i" -lt "$STORY_COUNT" ]; do
  key="$(jq -r ".issues[$i].key" "$DATA_FILE")"
  title="$(jq -r ".issues[$i].title" "$DATA_FILE")"
  body="$(jq -r ".issues[$i].body" "$DATA_FILE")"
  ms_key="$(jq -r ".issues[$i].milestone" "$DATA_FILE")"
  epic_key="$(jq -r ".issues[$i].epic" "$DATA_FILE")"
  points="$(jq -r ".issues[$i].points" "$DATA_FILE")"
  priority="$(jq -r ".issues[$i].priority" "$DATA_FILE")"
  labels=()
  while IFS= read -r l; do [ -n "$l" ] && labels+=("$l"); done \
    < <(jq -r ".issues[$i].labels[]" "$DATA_FILE")

  ms_title="$(milestone_title_for_key "$ms_key")"
  full_body="$body

---
<sub>\`$key\` · epic \`$epic_key\` · estimate ${points} pts · ${priority}</sub>"

  num="$(create_issue "$title" "$full_body" "$ms_title" "${labels[@]}")"
  if [ "$num" = "DRY" ]; then
    printf '  %s[dry-run]%s %s  %s\n' "$c_dim" "$c_reset" "$key" "$title"
  else
    prev="$(state_get "$key")"
    if [ "$prev" = "$num" ]; then skip "$key #$num"; else ok "$key #$num  $title"; fi
    state_set "$key" "$num"
  fi
  i=$((i + 1))
done

# --- epics (created after stories so they can link children) -----------------
EPIC_COUNT="$(jq '.epics | length' "$DATA_FILE")"
echo "  creating $EPIC_COUNT epics..."

j=0
while [ "$j" -lt "$EPIC_COUNT" ]; do
  ekey="$(jq -r ".epics[$j].key" "$DATA_FILE")"
  etitle="$(jq -r ".epics[$j].title" "$DATA_FILE")"
  ebody="$(jq -r ".epics[$j].body" "$DATA_FILE")"
  elabel="$(jq -r ".epics[$j].label" "$DATA_FILE")"
  ems_key="$(jq -r ".epics[$j].milestone" "$DATA_FILE")"
  ems_title="$(milestone_title_for_key "$ems_key")"

  children=""
  total_pts=0
  while IFS=$'\t' read -r ckey ctitle cpts; do
    [ -z "$ckey" ] && continue
    cnum="$(state_get "$ckey")"
    if [ -n "$cnum" ]; then
      children="$children
- [ ] #$cnum — $ctitle"
    else
      children="$children
- [ ] $ckey — $ctitle"
    fi
    total_pts=$((total_pts + cpts))
  done < <(jq -r --arg e "$ekey" '.issues[] | select(.epic==$e) | [.key, .title, (.points|tostring)] | @tsv' "$DATA_FILE")

  full_ebody="$ebody

## Stories in this epic
${children}

---
<sub>\`$ekey\` · ${total_pts} points total</sub>"

  enum="$(create_issue "$etitle" "$full_ebody" "$ems_title" "type:epic" "$elabel")"
  if [ "$enum" = "DRY" ]; then
    printf '  %s[dry-run]%s %s  %s\n' "$c_dim" "$c_reset" "$ekey" "$etitle"
  else
    ok "$ekey #$enum  $etitle (${total_pts} pts)"
    state_set "$ekey" "$enum"
  fi
  j=$((j + 1))
done

# ------------------------------------------------------- add to the board ----
step "Adding issues to the board"

if [ "$DRY_RUN" -eq 1 ]; then
  warn "skipped in dry-run"
else
  # Map issue number -> project item id, for everything already on the board.
  BOARD_JSON="$(gh project item-list "$PROJECT_NUMBER" --owner "$OWNER" --format json --limit 500)"
  board_item_id() {
    printf '%s' "$BOARD_JSON" | jq -r --argjson n "$1" \
      '.items[] | select(.content.number == $n) | .id' | head -1
  }

  add_and_set() { # $1 issue number, $2 sprint option, $3 epic option, $4 priority, $5 estimate
    local num="$1" sprint="$2" epic="$3" prio="$4" est="$5"
    local item_id; item_id="$(board_item_id "$num")"
    if [ -z "$item_id" ]; then
      item_id="$(gh project item-add "$PROJECT_NUMBER" --owner "$OWNER" \
                  --url "https://github.com/$REPO/issues/$num" --format json | jq -r '.id')"
    fi

    local oid
    if [ -n "$sprint" ]; then
      oid="$(option_id Sprint "$sprint")"
      [ -n "$oid" ] && gh project item-edit --id "$item_id" --project-id "$PROJECT_ID" \
        --field-id "$F_SPRINT" --single-select-option-id "$oid" >/dev/null
    fi
    if [ -n "$epic" ]; then
      oid="$(option_id Epic "$epic")"
      [ -n "$oid" ] && gh project item-edit --id "$item_id" --project-id "$PROJECT_ID" \
        --field-id "$F_EPIC" --single-select-option-id "$oid" >/dev/null
    fi
    if [ -n "$prio" ]; then
      oid="$(option_id Priority "$prio")"
      [ -n "$oid" ] && gh project item-edit --id "$item_id" --project-id "$PROJECT_ID" \
        --field-id "$F_PRIORITY" --single-select-option-id "$oid" >/dev/null
    fi
    if [ -n "$est" ] && [ "$est" != "null" ]; then
      gh project item-edit --id "$item_id" --project-id "$PROJECT_ID" \
        --field-id "$F_ESTIMATE" --number "$est" >/dev/null
    fi
    printf '%s' "$item_id"
  }

  i=0
  while [ "$i" -lt "$STORY_COUNT" ]; do
    key="$(jq -r ".issues[$i].key" "$DATA_FILE")"
    num="$(state_get "$key")"
    if [ -z "$num" ]; then warn "$key has no issue number, skipping board"; i=$((i + 1)); continue; fi
    ms_key="$(jq -r ".issues[$i].milestone" "$DATA_FILE")"
    epic_key="$(jq -r ".issues[$i].epic" "$DATA_FILE")"
    epic_opt="$(jq -r --arg k "$epic_key" '.epics[] | select(.key==$k) | .short' "$DATA_FILE")"
    add_and_set "$num" "$(sprint_option_for_key "$ms_key")" "$epic_opt" \
                "$(jq -r ".issues[$i].priority" "$DATA_FILE")" \
                "$(jq -r ".issues[$i].points" "$DATA_FILE")" >/dev/null
    ok "$key #$num on board"
    i=$((i + 1))
  done

  j=0
  while [ "$j" -lt "$EPIC_COUNT" ]; do
    ekey="$(jq -r ".epics[$j].key" "$DATA_FILE")"
    num="$(state_get "$ekey")"
    if [ -n "$num" ]; then
      ems_key="$(jq -r ".epics[$j].milestone" "$DATA_FILE")"
      epic_opt="$(jq -r ".epics[$j].short" "$DATA_FILE")"
      add_and_set "$num" "$(sprint_option_for_key "$ems_key")" "$epic_opt" "P0" "" >/dev/null
      ok "$ekey #$num on board"
    fi
    j=$((j + 1))
  done
fi

# ----------------------------------------------------------------- done -----
step "Done"

TOTAL_PTS="$(jq '[.issues[].points] | add' "$DATA_FILE")"
cat <<EOF

  Repository : https://github.com/$REPO
  Issues     : https://github.com/$REPO/issues
  Milestones : https://github.com/$REPO/milestones
  Board      : https://github.com/users/$OWNER/projects/$PROJECT_NUMBER

  ${STORY_COUNT} stories/tasks + ${EPIC_COUNT} epics, ${TOTAL_PTS} story points total.

  ${c_bold}Two things the CLI cannot do — finish these in the browser (2 minutes):${c_reset}

  1. Kanban columns. Open the board, click the Status field, and add
     "Ready", "In Review" and "Blocked" alongside the built-in
     Todo / In Progress / Done.

  2. Views. Add these saved views on the board:
       • "Sprint board"  — Board layout, group by Status, filter Sprint = current sprint
       • "By epic"       — Table layout, group by Epic, sum of Estimate
       • "Backlog"       — Table layout, filter Sprint = Backlog, sort by Priority

  State was written to $STATE_FILE — keep it if you want re-runs to stay cheap.

EOF
