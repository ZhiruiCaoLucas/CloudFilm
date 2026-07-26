#!/bin/sh
# ---------------------------------------------------------------------------
# Creates the Orthanc database and role alongside the CloudFilm database.
#
# Runs once, on first start of an empty PostgreSQL volume. CloudFilm and Orthanc
# share a database server but never a database: Orthanc's index is upstream
# software's schema and must stay outside CloudFilm's migration history.
# ---------------------------------------------------------------------------
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<SQL
    CREATE ROLE orthanc WITH LOGIN PASSWORD '${ORTHANC__POSTGRESQL__PASSWORD:-orthanc}';
    CREATE DATABASE orthanc OWNER orthanc;
SQL

echo "CloudFilm: created the 'orthanc' role and database."
