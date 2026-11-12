-- Event's World · database verification (TS-003, ADR-0002 deployment checklist).
-- Run after `migrate`, in the Supabase SQL Editor or with psql. Returns ONE table:
-- every row must show ok = true.

WITH module_schemas AS (
    SELECT n.nspname AS name, n.nspowner
    FROM pg_namespace n
    WHERE n.nspname IN ('accounts', 'events', 'registrations', 'payments',
                        'files', 'notifications', 'audit', 'legal')
),
checks (step, check_name, expected, actual) AS (
    SELECT 1, 'Module schemas owned by ew_migrator', 8,
           (SELECT count(*) FROM module_schemas WHERE pg_get_userbyid(nspowner) = 'ew_migrator')
    UNION ALL
    SELECT 2, 'Migration history tables (one per module)', 8,
           (SELECT count(*) FROM information_schema.tables t
            JOIN module_schemas s ON s.name = t.table_schema
            WHERE t.table_name = '__EFMigrationsHistory')
    UNION ALL
    SELECT 3, 'Module schemas usable by anon or authenticated', 0,
           (SELECT count(*) FROM module_schemas
            WHERE (EXISTS (SELECT FROM pg_roles WHERE rolname = 'anon')
                   AND has_schema_privilege('anon', name, 'USAGE'))
               OR (EXISTS (SELECT FROM pg_roles WHERE rolname = 'authenticated')
                   AND has_schema_privilege('authenticated', name, 'USAGE')))
    UNION ALL
    SELECT 4, 'Module schemas usable by ew_app', 8,
           (SELECT count(*) FROM module_schemas WHERE has_schema_privilege('ew_app', name, 'USAGE'))
    UNION ALL
    SELECT 5, 'Module schemas where ew_app can create objects', 0,
           (SELECT count(*) FROM module_schemas WHERE has_schema_privilege('ew_app', name, 'CREATE'))
    UNION ALL
    SELECT 6, 'Objects in the public schema', 0,
           (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public')
    UNION ALL
    SELECT 7, 'Extensions citext and pg_trgm', 2,
           (SELECT count(*) FROM pg_extension WHERE extname IN ('citext', 'pg_trgm'))
)
SELECT step, check_name, expected, actual, actual = expected AS ok
FROM checks
ORDER BY step;
