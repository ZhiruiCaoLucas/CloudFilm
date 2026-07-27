#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Collects everything needed to diagnose an Orthanc that starts but never
# listens, in one command, so the evidence arrives complete rather than in
# fragments.
#
#   ./scripts/diagnose-orthanc.sh
#   ./scripts/diagnose-orthanc.sh > orthanc-evidence.txt 2>&1
#
# Read-only: it inspects, it does not change anything.
# ---------------------------------------------------------------------------
set -uo pipefail

COMPOSE="docker compose -f docker-compose.yml -f docker-compose.dev.yml"

section() {
    printf '\n===== %s %s\n' "$1" "$(printf '=%.0s' $(seq 1 $((60 - ${#1}))))"
}

section "compose service state"
$COMPOSE ps

section "orthanc logs (full)"
$COMPOSE logs --no-color --tail 200 orthanc

section "generated configuration /tmp/orthanc.json"
# This is the ground truth. The image merges /etc/orthanc/*.json with every
# ORTHANC__* environment variable into this file, then starts Orthanc with it.
# If a setting is not in here, Orthanc never saw it.
$COMPOSE exec -T orthanc sh -c 'cat /tmp/orthanc.json 2>/dev/null || echo "(no /tmp/orthanc.json)"'

section "configuration files the image was given"
$COMPOSE exec -T orthanc sh -c 'ls -la /etc/orthanc/ 2>/dev/null || echo "(no /etc/orthanc)"'

section "plugins present on disk"
$COMPOSE exec -T orthanc sh -c 'ls -la /usr/share/orthanc/plugins/ 2>/dev/null || echo "(no plugin directory)"'

section "is anything listening on 8042 inside the container"
$COMPOSE exec -T orthanc sh -c \
    '(ss -lntp 2>/dev/null || netstat -lntp 2>/dev/null || echo "(no ss/netstat in image)")'

section "can orthanc authenticate against postgres"
# The failure this script was written for: the role password and the credential
# Orthanc uses were generated from different sources and did not match.
$COMPOSE exec -T postgres sh -c \
    'PGPASSWORD="$ORTHANC_DB_PASSWORD" psql -U orthanc -d orthanc -h 127.0.0.1 -tAc "select 1 as orthanc_login_ok" 2>&1' \
    || echo "(login FAILED - see the password mismatch note in docs/local-imaging.md Step 1)"

section "postgres roles and databases"
$COMPOSE exec -T postgres sh -c \
    'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\du" -c "\l" 2>&1'

section "orthanc process table"
$COMPOSE exec -T orthanc sh -c 'ps aux 2>/dev/null || ps 2>/dev/null || echo "(no ps in image)"'

printf '\nCollected. Paste this whole output back.\n'
