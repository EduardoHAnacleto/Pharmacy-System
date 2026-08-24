#!/usr/bin/env bash
# Diagnoses the .env at the repository root on Linux/macOS.
#
#   ./scripts/env-doctor.sh
#   ./scripts/env-doctor.sh /path/to/.env
#
# Reads nothing but the file and docker-compose.yml, changes nothing, and never
# prints a secret - only whether one is set and how long it is, so the output
# can be pasted into a ticket.
#
# The Windows twin, scripts/env-doctor.ps1, carries two extra checks for the
# ways the file breaks there. What is left is what matters on a server: a
# required variable that is missing or empty, a value compose silently rewrites,
# and a value compose accepts that the application then refuses.
#
# Deliberately free of associative arrays, so it runs on the bash 3.2 that
# macOS still ships.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

ENV_FILE="${1:-$REPO_ROOT/.env}"
IS_DEFAULT_PATH=0
[ $# -eq 0 ] && IS_DEFAULT_PATH=1

problems=0
warnings=0

if [ -t 1 ]; then
  RED=$'\033[31m'; YEL=$'\033[33m'; GRN=$'\033[32m'; CYA=$'\033[36m'; OFF=$'\033[0m'
else
  RED=''; YEL=''; GRN=''; CYA=''; OFF=''
fi

problem() { problems=$((problems + 1)); printf '  %s[problem]%s %s\n' "$RED" "$OFF" "$1"; }
warn()    { warnings=$((warnings + 1)); printf '  %s[warning]%s %s\n' "$YEL" "$OFF" "$1"; }
good()    { printf '  %s[ok]%s      %s\n' "$GRN" "$OFF" "$1"; }
info()    { printf '            %s\n' "$1"; }
section() { printf '\n%s%s%s\n' "$CYA" "$1" "$OFF"; }

# =========================================================================
section '1. The file'

shopt -s nullglob
candidates=("$REPO_ROOT"/.env*)
shopt -u nullglob
if [ ${#candidates[@]} -gt 0 ]; then
  info 'Files matching .env* in the repository root:'
  for c in "${candidates[@]}"; do
    [ -f "$c" ] || continue
    # The name is bracketed so a trailing space is visible.
    info "$(printf '  [%s] %s bytes' "$(basename "$c")" "$(wc -c < "$c" | tr -d ' ')")"
  done
fi

if [ ! -f "$ENV_FILE" ]; then
  problem "There is no file at $ENV_FILE"
  found_decoy=0
  for c in "${candidates[@]}"; do
    name="$(basename "$c")"
    case "$name" in
      .env|.env.example) continue ;;
    esac
    found_decoy=1
    info ''
    info "[$name] is not a name compose reads. It loads exactly [.env]."
  done
  if [ $found_decoy -eq 0 ]; then
    info ''
    info 'Start from the example, then fill in the blanks - copying it is not enough,'
    info 'the passwords ship empty on purpose:'
    info '    cp .env.example .env'
    info 'The required variables are listed in docs/OPERACOES.md section 1.'
  fi
  printf '\n%sFound %d problem(s).%s\n' "$RED" "$problems" "$OFF"
  exit 1
fi

good "[$(basename "$ENV_FILE")] exists, $(wc -c < "$ENV_FILE" | tr -d ' ') bytes"

# =========================================================================
section '2. Encoding'

if [ ! -s "$ENV_FILE" ]; then
  problem 'The file is empty.'
  printf '\n%sFound %d problem(s).%s\n' "$RED" "$problems" "$OFF"
  exit 1
fi

first4="$(head -c 4 "$ENV_FILE" | od -An -tx1 | tr -d ' \n')"
# bash cannot hold a NUL in a string, so `grep $'\x00'` searches for the empty
# string and matches every file. Compare the byte count with and without them.
has_nul=0
total_bytes="$(wc -c < "$ENV_FILE" | tr -d ' ')"
without_nul="$(LC_ALL=C tr -d '\000' < "$ENV_FILE" | wc -c | tr -d ' ')"
[ "$total_bytes" != "$without_nul" ] && has_nul=1

case "$first4" in
  fffe0000*) enc='UTF-32 little-endian' ;;
  fffe*)     enc='UTF-16 little-endian' ;;
  feff*)     enc='UTF-16 big-endian' ;;
  *)         if [ $has_nul -eq 1 ]; then enc='UTF-16 (no byte-order mark)'; else enc=''; fi ;;
esac

if [ -n "$enc" ]; then
  problem "The file is $enc. Compose cannot read it."
  info ''
  info 'Every variable comes out unset, so compose stops on the first `:?` guard'
  info 'and names a variable that is plainly there in the file.'
  info ''
  info 'Rewrite it as UTF-8, keeping a copy first:'
  info "    cp '$ENV_FILE' '$ENV_FILE.bak'"
  info "    iconv -f UTF-16 -t UTF-8 '$ENV_FILE.bak' > '$ENV_FILE'"
  info ''
  info 'Then run this script again. Nothing below could be checked.'
  printf '\n%sFound %d problem(s).%s\n' "$RED" "$problems" "$OFF"
  exit 1
fi

case "$first4" in
  efbbbf*) good 'UTF-8 with a byte-order mark. Compose handles this.' ;;
  *)       good 'UTF-8 (or plain ASCII), no byte-order mark.' ;;
esac

# =========================================================================
section '3. Contents'

KEYS=(); VALS=(); DOLLARS=(); LINENOS=()

index_of() {
  local want=$1 i
  for ((i = 0; i < ${#KEYS[@]}; i++)); do
    if [ "${KEYS[$i]}" = "$want" ]; then printf '%s' "$i"; return 0; fi
  done
  return 1
}

lineno=0
count=0
dupe_report=''
syntax_issues=0

# Redirected, not piped: a pipe would run the loop in a subshell and every
# counter incremented here would be discarded at the end of it.
while IFS= read -r raw || [ -n "$raw" ]; do
  lineno=$((lineno + 1))
  line="${raw%$'\r'}"                         # tolerate CRLF; compose does
  trimmed="${line#"${line%%[![:space:]]*}"}"  # ltrim
  trimmed="${trimmed%"${trimmed##*[![:space:]]}"}"
  [ -z "$trimmed" ] && continue
  case "$trimmed" in \#*) continue ;; esac
  case "$trimmed" in 'export '*) trimmed="${trimmed#export }" ;; esac
  case "$trimmed" in
    *=*) ;;
    *) problem "Line $lineno: not a KEY=VALUE assignment and not a comment."; continue ;;
  esac

  key="${trimmed%%=*}"
  value="${trimmed#*=}"
  key="${key%"${key##*[![:space:]]}"}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"

  if ! printf '%s' "$key" | grep -qE '^[A-Za-z_][A-Za-z0-9_]*$'; then
    problem "Line $lineno: [$key] is not a usable variable name."
    continue
  fi

  quote='none'
  case "$value" in
    "'"*"'") quote='single'; value="${value%\'}"; value="${value#\'}" ;;
    '"'*'"') quote='double'; value="${value%\"}"; value="${value#\"}" ;;
  esac

  # Whitespace then `#` starts a comment in an unquoted value; a `#` glued to
  # the value does not. Cut it here, so every length reported below is what
  # compose passes to the container, not what the line looks like.
  if [ "$quote" = 'none' ] && printf '%s' "$value" | grep -qE '[[:space:]]#'; then
    value="$(printf '%s' "$value" | sed -E 's/[[:space:]]+#.*$//')"
    syntax_issues=$((syntax_issues + 1))
    problem "Line $lineno: $key contains a space followed by #, which starts a comment."
    info '          Compose keeps only what is before it. Remove the space, or quote'
    info "          the value. It reaches the stack as ${#value} character(s)."
  fi

  # `$` starts an interpolation unless doubled or wrapped in single quotes.
  # Double quotes do NOT protect it.
  has_dollar=0
  if [ "$quote" != 'single' ]; then
    stripped="${value//\$\$/}"
    case "$stripped" in
      *'$'*)
        has_dollar=1
        syntax_issues=$((syntax_issues + 1))
        problem "Line $lineno: $key contains a single \$, which compose expands."
        info '          Everything from the $ onwards is replaced, usually by nothing.'
        info '          Write it as $$, or wrap the whole value in single quotes -'
        info '          double quotes do not protect it.'
        ;;
    esac
  fi

  count=$((count + 1))
  if i="$(index_of "$key")"; then
    dupe_report="${dupe_report}${key} (lines ${LINENOS[$i]}, ${lineno})"$'\n'
    VALS[$i]="$value"; DOLLARS[$i]=$has_dollar; LINENOS[$i]=$lineno   # last wins
  else
    KEYS+=("$key"); VALS+=("$value"); DOLLARS+=("$has_dollar"); LINENOS+=("$lineno")
  fi
done < "$ENV_FILE"

info "$count assignment(s) read."
if [ -n "$dupe_report" ]; then
  while IFS= read -r d; do
    [ -n "$d" ] && warn "$d is set more than once. The last one wins."
  done <<< "$dupe_report"
fi

# =========================================================================
section '4. Characters compose treats as syntax'
if [ $syntax_issues -eq 0 ]; then
  good 'No value contains a character compose would reinterpret.'
else
  info "$syntax_issues reported above, in the order they appear."
fi

# =========================================================================
section '5. Required variables'

# Read the guard list out of docker-compose.yml rather than repeating it here,
# so this check cannot drift from the file that enforces it.
required="$(grep -oE '\$\{[A-Za-z_][A-Za-z0-9_]*:\?' docker-compose.yml 2>/dev/null \
            | sed 's/^\${//; s/:?$//' | sort -u)"
if [ -z "$required" ]; then
  warn 'Could not read the guards from docker-compose.yml; using the documented list.'
  required=$'CORS_ALLOWED_ORIGINS\nJWT_SIGNING_KEY\nMYSQL_DATABASE\nMYSQL_PASSWORD\nMYSQL_ROOT_PASSWORD\nMYSQL_USER\nREDIS_PASSWORD'
fi

while IFS= read -r name; do
  [ -z "$name" ] && continue
  if ! i="$(index_of "$name")"; then
    problem "$name is not in the file. Compose stops before starting anything."
  elif [ -z "${VALS[$i]}" ]; then
    problem "$name is present but empty. A ':?' guard treats that the same as absent."
  elif [ "${DOLLARS[$i]}" = "1" ]; then
    warn "$name contains a \$; what the stack receives cannot be read off the file."
  else
    good "$(printf '%-22s set (%d characters)' "$name" "${#VALS[$i]}")"
  fi
done <<< "$required"

# =========================================================================
section '6. Values compose accepts but the application does not'

# The API validates this itself at startup and exits if it is too short, well
# after compose has reported success.
if i="$(index_of JWT_SIGNING_KEY)" && [ "${DOLLARS[$i]}" != "1" ]; then
  jwt_len=${#VALS[$i]}
  if [ "$jwt_len" -gt 0 ] && [ "$jwt_len" -lt 32 ]; then
    problem "JWT_SIGNING_KEY is $jwt_len characters; the API requires at least 32 and exits on less."
    info '          The database and Redis come up, then the backend container stops.'
  elif [ "$jwt_len" -ge 32 ]; then
    good "JWT_SIGNING_KEY is long enough ($jwt_len characters, minimum 32)."
  fi
fi

# The MySQL entrypoint creates MYSQL_USER itself and refuses to create root.
if i="$(index_of MYSQL_USER)" && [ "${VALS[$i]}" = 'root' ]; then
  problem 'MYSQL_USER is root. The MySQL entrypoint refuses to create that account.'
  info '          Use an application user, for example storefront.'
fi

if i="$(index_of MYSQL_PASSWORD)" && j="$(index_of MYSQL_ROOT_PASSWORD)"; then
  if [ -n "${VALS[$i]}" ] && [ "${VALS[$i]}" = "${VALS[$j]}" ]; then
    warn 'MYSQL_PASSWORD is the same as MYSQL_ROOT_PASSWORD; they are meant to differ.'
  fi
fi

# Deliberately unguarded in docker-compose.yml, because a redeploy over a
# database that already has its admin does not need them. On a first install an
# empty pair means no account is created and there is no way to log in.
seed_user=''; seed_pass=''
i="$(index_of ADMIN_SEED_USERNAME)" && seed_user="${VALS[$i]}"
i="$(index_of ADMIN_SEED_PASSWORD)" && seed_pass="${VALS[$i]}"
if [ -z "$seed_user" ] || [ -z "$seed_pass" ]; then
  warn 'ADMIN_SEED_USERNAME/ADMIN_SEED_PASSWORD are not both set.'
  info '          The stack will start, but on a new database no admin account is'
  info '          created and there is no way to log in. Harmless if this database'
  info '          already has its admin - the pair is only read while users is empty.'
else
  good 'ADMIN_SEED_USERNAME/ADMIN_SEED_PASSWORD are both set.'
fi

# =========================================================================
# `ls-files -- .env` lists the path when tracked and prints nothing when not.
if [ -d "$REPO_ROOT/.git" ] && command -v git >/dev/null 2>&1; then
  if [ -n "$(git ls-files -- .env)" ]; then
    section '7. Version control'
    problem '.env is tracked by git. It holds secrets and must not be.'
    info '          git rm --cached .env'
    info '          Then rotate everything in it: the history still has the old values.'
  fi
fi

# =========================================================================
section '8. Cross-check with compose'
if ! command -v docker >/dev/null 2>&1; then
  info 'docker is not on PATH; skipping.'
else
  # --quiet keeps the resolved configuration - passwords included - out of the
  # scrollback. Compose still prints the reason on failure.
  if [ $IS_DEFAULT_PATH -eq 1 ]; then
    docker compose config --quiet
  else
    docker compose --env-file "$ENV_FILE" config --quiet
  fi
  if [ $? -eq 0 ]; then
    good 'docker compose config resolved the whole file.'
  else
    problem 'docker compose config failed; its reason is printed above.'
  fi
fi

# =========================================================================
echo
if [ $problems -gt 0 ]; then
  printf '%s%d problem(s), %d warning(s).%s\n' "$RED" "$problems" "$warnings" "$OFF"
  echo 'The stack will not come up until the problems are fixed.'
  exit 1
fi
if [ $warnings -gt 0 ]; then
  printf '%sNo problems, %d warning(s).%s\n' "$YEL" "$warnings" "$OFF"
  echo 'The stack should come up. Read the warnings before trusting it.'
  exit 0
fi
printf '%sNo problems found.%s\n' "$GRN" "$OFF"
echo 'If the stack still does not start, the cause is not this file:'
echo '  docker compose up -d --build'
echo '  docker compose logs --tail 50'
exit 0
