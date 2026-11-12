-- Event's World · database bootstrap (TS-003, ADR-0002, database.md §5–6).
--
-- Run ONCE per database as an administrator (Supabase: SQL Editor as `postgres`).
-- It is idempotent, so running it again is safe. It creates the two application roles and the
-- database-level privileges they need. Schemas, grants per schema and extensions are created
-- by the EF Core migrations, which run as ew_migrator.
--
-- Passwords are NOT set here (nothing secret lives in the repository). Set them afterwards:
--   ALTER ROLE ew_migrator PASSWORD '<from the secrets manager>';
--   ALTER ROLE ew_app      PASSWORD '<from the secrets manager>';

DO $$
BEGIN
    -- Owns the module schemas and runs the migrations (DDL).
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ew_migrator') THEN
        CREATE ROLE ew_migrator LOGIN NOINHERIT;
    END IF;

    -- Used by the API: data access only (DML), no DDL.
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ew_app') THEN
        CREATE ROLE ew_app LOGIN NOINHERIT;
    END IF;

    EXECUTE format('GRANT CONNECT, CREATE ON DATABASE %I TO ew_migrator', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO ew_app', current_database());
END
$$;

-- Supabase keeps extensions in the `extensions` schema. They are installed here, by the
-- administrator, because Supabase controls which roles may run CREATE EXTENSION. The migrations
-- repeat `CREATE EXTENSION IF NOT EXISTS`, which is then a no-op.
CREATE SCHEMA IF NOT EXISTS extensions;
CREATE EXTENSION IF NOT EXISTS citext WITH SCHEMA extensions;
CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA extensions;
GRANT USAGE, CREATE ON SCHEMA extensions TO ew_migrator;
GRANT USAGE ON SCHEMA extensions TO ew_app;

ALTER ROLE ew_migrator SET search_path TO public, extensions;
ALTER ROLE ew_app SET search_path TO public, extensions;

-- `public` stays empty: neither role may create objects in it.
REVOKE CREATE ON SCHEMA public FROM ew_migrator, ew_app;
