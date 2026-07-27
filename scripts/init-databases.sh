#!/bin/sh
# ---------------------------------------------------------------------------
# Creates the Orthanc role and database alongside the CloudFilm database.
#
# Runs inside the POSTGRES container, on first start of an empty data volume.
# CloudFilm and Orthanc share a server but never a database: Orthanc's index is
# upstream software's schema and must stay outside CloudFilm's migration history.
#
# The password is read from ORTHANC_DB_PASSWORD, which docker-compose.yml passes
# to BOTH this container and Orthanc. It used to read ORTHANC__POSTGRESQL__PASSWORD,
# which is only defined on the Orthanc service - so here it was always unset, the
# fallback silently won, and the role ended up with a different password from the
# one Orthanc connects with. The plugin then retried the connection forever and
# Orthanc never opened its HTTP port.
#
# There is deliberately no default now: a missing value must fail loudly.
# ---------------------------------------------------------------------------
set -eu

: "${ORTHANC_DB_PASSWORD:?ORTHANC_DB_PASSWORD must be set on the postgres service}"

run_sql() {
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -c "$1"
}

query() {
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -tAc "$1"
}

# The password is set on every run, not only at creation, so re-running this
# repairs a role whose password has drifted rather than skipping it.
if [ "$(query "SELECT 1 FROM pg_roles WHERE rolname = 'orthanc'")" = "1" ]; then
    run_sql "ALTER ROLE orthanc WITH LOGIN PASSWORD '${ORTHANC_DB_PASSWORD}'"
    echo "CloudFilm: updated the password of the existing 'orthanc' role."
else
    run_sql "CREATE ROLE orthanc WITH LOGIN PASSWORD '${ORTHANC_DB_PASSWORD}'"
    echo "CloudFilm: created the 'orthanc' role."
fi

if [ "$(query "SELECT 1 FROM pg_database WHERE datname = 'orthanc'")" != "1" ]; then
    run_sql "CREATE DATABASE orthanc OWNER orthanc"
    echo "CloudFilm: created the 'orthanc' database."
fi
