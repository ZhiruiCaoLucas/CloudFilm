#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Creates a .env with freshly generated secrets.
# Refuses to overwrite an existing .env - rotating a live deployment's secrets
# must be a deliberate act, not a side effect of running a helper script.
# ---------------------------------------------------------------------------
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="$repo_root/.env"

if [[ -f "$env_file" ]]; then
  echo "error: $env_file already exists. Remove it first if you intend to regenerate secrets." >&2
  exit 1
fi

secret() { openssl rand -base64 36 | tr -d '\n/+=' | cut -c1-40; }

sed \
  -e "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(secret)|" \
  -e "s|^ORTHANC_PASSWORD=.*|ORTHANC_PASSWORD=$(secret)|" \
  -e "s|^ORTHANC_DB_PASSWORD=.*|ORTHANC_DB_PASSWORD=$(secret)|" \
  -e "s|^JWT_SIGNING_KEY=.*|JWT_SIGNING_KEY=$(secret)|" \
  "$repo_root/.env.example" > "$env_file"

chmod 600 "$env_file"
echo "Wrote $env_file with generated secrets."
