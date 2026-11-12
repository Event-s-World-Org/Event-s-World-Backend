using System.Text.RegularExpressions;

namespace EventsWorld.Shared.Infrastructure.Persistence;

/// <summary>Guards identifiers that are written into raw SQL (schemas, extensions).</summary>
internal static partial class SqlIdentifier
{
    public static void EnsureValid(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || !LowerSnakeCase().IsMatch(identifier))
        {
            throw new ArgumentException(
                $"'{identifier}' is not a valid lower snake_case SQL identifier.", nameof(identifier));
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex LowerSnakeCase();
}
