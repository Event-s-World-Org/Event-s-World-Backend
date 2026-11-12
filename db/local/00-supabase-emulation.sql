-- LOCAL ONLY (Docker and integration tests). Never run on Supabase.
-- Recreates the Supabase pieces our rules depend on, so local databases behave like the real one:
-- the Data API roles `anon` and `authenticated`, which must never reach our schemas.

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'anon') THEN
        CREATE ROLE anon NOLOGIN;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'authenticated') THEN
        CREATE ROLE authenticated NOLOGIN;
    END IF;
END
$$;

-- Like Supabase, the Data API roles can use the public schema.
GRANT USAGE ON SCHEMA public TO anon, authenticated;
