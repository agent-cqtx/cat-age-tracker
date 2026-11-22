#!/usr/bin/env bash
#
# Integration regression suite for cat-tracker (attachments + appointment CRUD).
#
# Targets the ISOLATED test instance by default (http://localhost:8094).
# Override with:  BASE_URL=http://host:port ./run-tests.sh
#
# HARD GUARD: refuses to run against port 8093 — that is the user's LIVE
# instance. This suite POSTs, PUTs and DELETEs; those must never touch live data.
#
set -u

# ---- Configuration ----------------------------------------------------------
BASE_URL="${BASE_URL:-http://localhost:8094}"
TESTPW_FILE="${TESTPW_FILE:-/home/agent-x/.hermes/cache/scratch/cat-tracker-test/testpw}"
# Host-side view of the container's /app/data volume (read-only use only:
# blob-existence assertions after deletes).
DATA_DIR="${DATA_DIR:-/home/agent-x/.hermes/cache/scratch/cat-tracker-test/data}"
SCRATCH="${SCRATCH_DIR:-/home/agent-x/.hermes/cache/scratch}"
PREFIX="ZZ-AUTOTEST"          # marks every fixture this suite creates

# ---- Guards ------------------------------------------------------------------
if [[ "$BASE_URL" =~ :8093(/|$|[^0-9]) ]]; then
  echo "REFUSING TO RUN: BASE_URL=$BASE_URL points at port 8093." >&2
  echo "Port 8093 is the user's LIVE instance. This suite performs writes and" >&2
  echo "deletes. Point BASE_URL at the isolated test instance (8094) instead." >&2
  exit 2
fi
case "$(realpath -m "$DATA_DIR" 2>/dev/null || echo "$DATA_DIR")" in
  */dev-projects/cat-tracker/data)
    echo "REFUSING TO RUN: DATA_DIR=$DATA_DIR is the LIVE project data dir." >&2
    exit 2 ;;
esac

if [[ ! -r "$TESTPW_FILE" ]]; then
  echo "Cannot read password file: $TESTPW_FILE" >&2
  exit 2
fi
PASSWORD="$(cat "$TESTPW_FILE")"
PY="$(command -v python3 || echo /usr/bin/python3)"

# ---- Temp workspace -----------------------------------------------------------
TMP="$(mktemp -d "$SCRATCH/cat-tracker-tests.XXXXXX")"
JAR="$TMP/cookies.txt"
trap 'rm -rf "$TMP"' EXIT

# ---- Fixtures (generated at runtime) ------------------------------------------
FX="$TMP/fixtures"
mkdir -p "$FX"
# Valid 1x1 PNG
printf 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4\n2mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==\n' \
  | sed 's/[[:space:]]//g' | base64 -d > "$FX/$PREFIX-general.png"
cp "$FX/$PREFIX-general.png" "$FX/$PREFIX-appt.png"
# Valid minimal PDF
printf '%%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj\ntrailer<</Root 1 0 R>>\n%%%%EOF\n' > "$FX/$PREFIX-doc.pdf"
# Junk txt (must be rejected: text/plain not allowed)
echo "this is junk $(date +%s)" > "$FX/rejected.txt"
# Oversized PDF-ish file: 11 MB (> 10 MB limit)
{ printf '%%PDF-1.4\n'; head -c 11554304 /dev/zero; } > "$FX/$PREFIX-oversize.pdf"

# ---- Helpers -------------------------------------------------------------------
PASS=0; FAIL=0; FAILED_NAMES=()
report() { # report <name> <ok:0|1> <detail>
  if [[ "$2" == "0" ]]; then
    PASS=$((PASS+1)); printf 'PASS  %s\n' "$1"
  else
    FAIL=$((FAIL+1)); FAILED_NAMES+=("$1"); printf 'FAIL  %s — %s\n' "$1" "$3"
  fi
}
assert_eq() { # assert_eq <name> <expected> <actual>
  if [[ "$2" == "$3" ]]; then report "$1" 0 ""
  else report "$1" 1 "expected [$2] got [$3]"; fi
}
assert_true() { # assert_true <name> <detail-on-fail> <cmd...>
  local name="$1" detail="$2"; shift 2
  if "$@" >/dev/null 2>&1; then report "$name" 0 ""; else report "$name" 1 "$detail"; fi
}
# api <method> <path> [curl args...]  -> sets CODE, BODY, HDR ($TMP/hdr)
api() {
  local method="$1" path="$2"; shift 2
  CODE="$(curl -s -X "$method" "$BASE_URL$path" "$@" -o "$TMP/body" -D "$TMP/hdr" -w '%{http_code}' -sS 2>/dev/null || echo 000)"
  BODY="$(tr -d '\0' < "$TMP/body" 2>/dev/null)"
}
jget() { # jget <json> <python-subscript-expr on obj>   e.g. jget "$BODY" '["id"]'
  printf '%s' "$1" | "$PY" -c "import json,sys;print(json.load(sys.stdin)$2)" 2>/dev/null
}
noguid() { printf '%s' "$1" | tr -d '-' | tr 'A-Z' 'a-z'; }

login() {
  api POST /api/auth/login -H 'Content-Type: application/json' \
      -d "{\"password\":\"$PASSWORD\"}" -c "$JAR"
}
sweep_fixtures() { # remove leftovers of OUR prefix (best effort, idempotent)
  local id
  for id in $(curl -s -b "$JAR" "$BASE_URL/api/appointments" \
      | "$PY" -c 'import json,sys
for a in json.load(sys.stdin):
    if a["title"].startswith("'"$PREFIX"'"): print(a["id"])' 2>/dev/null); do
    curl -s -b "$JAR" -X DELETE "$BASE_URL/api/appointments/$id" -o /dev/null
  done
  for id in $(curl -s -b "$JAR" "$BASE_URL/api/attachments" \
      | "$PY" -c 'import json,sys
for a in json.load(sys.stdin):
    if a["fileName"].startswith("'"$PREFIX"'"): print(a["id"])' 2>/dev/null); do
    curl -s -b "$JAR" -X DELETE "$BASE_URL/api/attachments/$id" -o /dev/null
  done
}

echo "=== cat-tracker integration suite ==="
echo "BASE_URL=$BASE_URL  DATA_DIR=$DATA_DIR"

# Health check before anything else
api GET /api/auth/status
assert_eq "00 server reachable (/api/auth/status)" "200" "$CODE"
if [[ "$CODE" != "200" ]]; then
  echo "Test instance unreachable at $BASE_URL — aborting." >&2
  echo "PASS=$PASS FAIL=$FAIL"; exit 1
fi

# Pre-clean stale fixtures from earlier interrupted runs
login; sweep_fixtures

# ---- Auth ----------------------------------------------------------------------
api POST /api/auth/login -H 'Content-Type: application/json' \
    -d '{"password":"definitely-not-the-password-9x7"}'
assert_eq "01 login wrong password -> 401" "401" "$CODE"

login
assert_eq "02 login correct password -> 200" "200" "$CODE"
assert_eq "03 login response authenticated=true" "True" "$(jget "$BODY" '['"'"'authenticated'"'"']')"
assert_true "04 auth cookie 'cat_tracker_auth' in jar" "cookie missing" grep -q cat_tracker_auth "$JAR"
api GET /api/auth/status -b "$JAR"
assert_eq "05 auth status authenticated=true with cookie" "True" \
  "$(jget "$BODY" '['"'"'authenticated'"'"']')"

# ---- Appointment CRUD round-trip -----------------------------------------------
api POST /api/appointments -b "$JAR" -H 'Content-Type: application/json' \
    -d "{\"title\":\"$PREFIX appt\",\"notes\":\"orig notes\",\"when\":\"2026-12-01T10:00:00\",\"category\":\"Vet\"}"
assert_eq "06 POST appointment -> 201" "201" "$CODE"
APPT1="$(jget "$BODY" '['"'"'id'"'"']')"
assert_eq "07 created title matches" "$PREFIX appt" "$(jget "$BODY" '['"'"'title'"'"']')"

api GET /api/appointments
assert_eq "08 appointment appears in list" "1" \
  "$("$PY" -c 'import json,sys;print(sum(1 for a in json.load(sys.stdin) if a["title"]=="'"$PREFIX"' appt"))' <<<"$BODY")"

api PUT "/api/appointments/$APPT1" -b "$JAR" -H 'Content-Type: application/json' \
    -d "{\"title\":\"$PREFIX appt edited\",\"notes\":null,\"when\":\"2026-12-02T14:30:00\",\"category\":\"Grooming\"}"
assert_eq "09 PUT appointment -> 200" "200" "$CODE"
assert_eq "10 updated title returned" "$PREFIX appt edited" "$(jget "$BODY" '['"'"'title'"'"']')"
assert_eq "11 cleared notes -> null" "None" "$(jget "$BODY" '['"'"'notes'"'"']')"

# ---- General attachment lifecycle ------------------------------------------------
api POST /api/attachments -b "$JAR" -F "file=@$FX/$PREFIX-general.png;type=image/png"
assert_eq "12 upload general PNG -> 201" "201" "$CODE"
GATT="$(jget "$BODY" '['"'"'id'"'"']')"
SRC_MD5="$(md5sum "$FX/$PREFIX-general.png" | cut -d' ' -f1)"
SRC_SIZE="$(stat -c%s "$FX/$PREFIX-general.png")"
assert_eq "13 metadata sizeBytes matches file" "$SRC_SIZE" "$(jget "$BODY" '['"'"'sizeBytes'"'"']')"
assert_eq "14 metadata fileName matches (basename only)" "$PREFIX-general.png" "$(jget "$BODY" '['"'"'fileName'"'"']')"
assert_true "15 Location header points at attachment" "no Location" grep -qi "^location: /api/attachments/$GATT" "$TMP/hdr"

api GET /api/attachments
assert_eq "16 general list contains new attachment" "1" \
  "$("$PY" -c 'import json,sys;print(sum(1 for a in json.load(sys.stdin) if a["id"]=="'"$GATT"'"))' <<<"$BODY")"

api GET "/api/attachments/$GATT/file"
assert_eq "17 download general attachment -> 200" "200" "$CODE"
assert_eq "18 downloaded bytes md5-identical to upload" "$SRC_MD5" "$(md5sum "$TMP/body" | cut -d' ' -f1)"
assert_true "19 download content-type image/png" "wrong type" grep -qi 'content-type: image/png' "$TMP/hdr"
assert_true "20 download sets nosniff" "no nosniff" grep -qi 'x-content-type-options: nosniff' "$TMP/hdr"

api POST /api/attachments -b "$JAR" -F "file=@$FX/$PREFIX-doc.pdf;type=application/pdf"
assert_eq "21 upload general PDF -> 201" "201" "$CODE"
GPDF="$(jget "$BODY" '['"'"'id'"'"']')"

api DELETE "/api/attachments/$GATT" -b "$JAR"
assert_eq "22 delete general attachment -> 204" "204" "$CODE"
api GET /api/attachments
assert_eq "23 deleted attachment gone from list" "0" \
  "$("$PY" -c 'import json,sys;print(sum(1 for a in json.load(sys.stdin) if a["id"]=="'"$GATT"'"))' <<<"$BODY")"
api GET "/api/attachments/$GATT/file"
assert_eq "24 deleted attachment file -> 404" "404" "$CODE"
assert_true "25 blob file removed from disk (general delete)" "blob left on disk" \
  test ! -e "$DATA_DIR/attachments/$(noguid "$GATT")"
api DELETE "/api/attachments/$GATT" -b "$JAR"
assert_eq "26 double-delete -> 404" "404" "$CODE"

# ---- Appointment attachment lifecycle + cascade -----------------------------------
api POST /api/appointments -b "$JAR" -H 'Content-Type: application/json' \
    -d "{\"title\":\"$PREFIX cascade\",\"notes\":null,\"when\":\"2026-12-05T09:00:00\",\"category\":\"Other\"}"
APPT2="$(jget "$BODY" '['"'"'id'"'"']')"
assert_eq "27 second fixture appointment created" "201" "$CODE"

api POST "/api/appointments/$APPT2/attachments" -b "$JAR" -F "file=@$FX/$PREFIX-appt.png;type=image/png"
assert_eq "28 upload appointment attachment -> 201" "201" "$CODE"
AATT="$(jget "$BODY" '['"'"'id'"'"']')"
assert_eq "29 attachment linked to appointment id" "$APPT2" "$(jget "$BODY" '['"'"'appointmentId'"'"']')"

api POST "/api/appointments/$APPT2/attachments" -b "$JAR" -F "file=@$FX/$PREFIX-doc.pdf;type=application/pdf"
APDF="$(jget "$BODY" '['"'"'id'"'"']')"

api GET "/api/appointments/$APPT2/attachments"
assert_eq "30 appointment attachment list -> 200" "200" "$CODE"
assert_eq "31 appointment list contains both attachments" "2" \
  "$("$PY" -c 'import json,sys;print(len([a for a in json.load(sys.stdin) if a["appointmentId"]=="'"$APPT2"'"]))' <<<"$BODY")"

api GET "/api/attachments/$AATT/file"
assert_eq "32 appointment attachment download md5-identical" "$SRC_MD5" "$(md5sum "$TMP/body" | cut -d' ' -f1)"

api GET /api/attachments
assert_eq "33 appointment attachments NOT in general list" "0" \
  "$("$PY" -c 'import json,sys;print(sum(1 for a in json.load(sys.stdin) if a["id"] in ("'"$AATT"'","'"$APDF"'")))' <<<"$BODY")"

# cascade: deleting the appointment removes metadata AND blobs on disk
api DELETE "/api/appointments/$APPT2" -b "$JAR"
assert_eq "34 delete appointment -> 204" "204" "$CODE"
api GET "/api/appointments/$APPT2/attachments"
assert_eq "35 appointment attachment list -> 404 after delete" "404" "$CODE"
api GET "/api/attachments/$AATT/file"
assert_eq "36 cascade: attachment file -> 404" "404" "$CODE"
assert_true "37 cascade: png blob removed from disk" "blob left on disk" \
  test ! -e "$DATA_DIR/attachments/$(noguid "$AATT")"
assert_true "38 cascade: pdf blob removed from disk" "blob left on disk" \
  test ! -e "$DATA_DIR/attachments/$(noguid "$APDF")"

# ---- Validation matrix ------------------------------------------------------------
# (run with cookie jar freshly emptied for the unauthenticated case)
rm -f "$TMP/nojar"; : > "$TMP/nojar"
api POST /api/attachments -b "$TMP/nojar" -F "file=@$FX/$PREFIX-general.png;type=image/png"
assert_eq "39 unauthenticated upload -> 401" "401" "$CODE"
api POST /api/appointments -b "$TMP/nojar" -H 'Content-Type: application/json' \
    -d "{\"title\":\"$PREFIX should-not-exist\",\"when\":\"2026-12-01T10:00:00\",\"category\":\"Vet\"}"
assert_eq "40 unauthenticated appointment create -> 401" "401" "$CODE"

api POST /api/attachments -b "$JAR" -F "file=@$FX/rejected.txt;type=text/plain"
assert_eq "41 text/plain upload -> 415" "415" "$CODE"
api POST /api/attachments -b "$JAR" -F "file=@$FX/$PREFIX-oversize.pdf;type=application/pdf"
assert_eq "42 >10MB upload -> 413" "413" "$CODE"
api POST "/api/appointments/0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f/attachments" \
    -b "$JAR" -F "file=@$FX/$PREFIX-general.png;type=image/png"
assert_eq "43 upload to nonexistent appointment -> 404" "404" "$CODE"
api POST /api/attachments -b "$JAR" -H 'Content-Type: application/json' -d '{}'
assert_eq "44 non-multipart upload -> 400" "400" "$CODE"
api POST /api/appointments -b "$JAR" -H 'Content-Type: application/json' \
    -d "{\"title\":\"   \",\"when\":\"2026-12-01T10:00:00\",\"category\":\"Vet\"}"
assert_eq "45 blank-title appointment -> 400" "400" "$CODE"

# ---- Logout -------------------------------------------------------------------------
api POST /api/auth/logout -b "$JAR" -c "$JAR"
assert_eq "46 logout -> 200" "200" "$CODE"
api GET /api/auth/status -b "$JAR"
assert_eq "47 status authenticated=false after logout" "False" \
  "$(jget "$BODY" '['"'"'authenticated'"'"']')"
api DELETE "/api/attachments/$GPDF" -b "$JAR"
assert_eq "48 delete after logout -> 401" "401" "$CODE"

# ---- Re-login + idempotent fixture cleanup --------------------------------------------
login
assert_eq "49 re-login after suite -> 200" "200" "$CODE"
sweep_fixtures
api GET /api/appointments
assert_eq "50 fixtures swept: no ZZ-AUTOTEST appointments left" "0" \
  "$("$PY" -c 'import json,sys;print(sum(1 for a in json.load(sys.stdin) if a["title"].startswith("'"$PREFIX"'")))' <<<"$BODY")"
api GET /api/attachments
assert_eq "51 fixtures swept: no ZZ-AUTOTEST attachments left" "0" \
  "$("$PY" -c 'import json,sys;print(sum(1 for a in json.load(sys.stdin) if a["fileName"].startswith("'"$PREFIX"'")))' <<<"$BODY")"

# ---- Summary ------------------------------------------------------------------------
echo "-----------------------------------------"
echo "PASS=$PASS FAIL=$FAIL"
if (( FAIL > 0 )); then
  printf 'failed: %s\n' "${FAILED_NAMES[@]}"
  exit 1
fi
exit 0
