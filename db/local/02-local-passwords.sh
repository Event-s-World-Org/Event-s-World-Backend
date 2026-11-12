#!/bin/sh
# LOCAL ONLY. Sets the role passwords from the variables in .env (see .env.example).
# Sourced by the official postgres image on the first start of an empty volume.
set -eu

psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    -v migrator_password="$EW_MIGRATOR_PASSWORD" \
    -v app_password="$EW_APP_PASSWORD" <<'SQL'
ALTER ROLE ew_migrator PASSWORD :'migrator_password';
ALTER ROLE ew_app PASSWORD :'app_password';
SQL
