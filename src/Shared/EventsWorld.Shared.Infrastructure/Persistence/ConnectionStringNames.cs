namespace EventsWorld.Shared.Infrastructure.Persistence;

/// <summary>
/// Names under <c>ConnectionStrings</c> (user secrets locally, environment variables elsewhere,
/// e.g. <c>ConnectionStrings__Database</c>). Never stored in appsettings files.
/// </summary>
public static class ConnectionStringNames
{
    /// <summary>The API, as <c>ew_app</c>, through the Supabase pooler in transaction mode.</summary>
    public const string Database = "Database";

    /// <summary>Migrations, as <c>ew_migrator</c>, through a direct or session-mode connection.</summary>
    public const string Migrations = "Migrations";
}
