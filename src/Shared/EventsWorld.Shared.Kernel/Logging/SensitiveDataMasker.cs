using System.Text.RegularExpressions;

namespace EventsWorld.Shared.Kernel.Logging;

public static partial class SensitiveDataMasker
{
    public const string RedactedEmail = "***@***.***";
    public const string RedactedToken = "[REDACTED_TOKEN]";

    // Regex for email addresses
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // Regex for Bearer tokens
    [GeneratedRegex(@"(?i)\b(bearer\s+)[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled)]
    private static partial Regex BearerTokenRegex();

    // Regex for token assignment patterns (e.g. token=xyz, "token": "xyz")
    [GeneratedRegex(@"(?i)(""?(?:access_token|refresh_token|token|password|secret|authorization)""?\s*[:=]\s*""?)[^""\s,;]+(""?)", RegexOptions.Compiled)]
    private static partial Regex TokenKeyValueRegex();

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email",
        "EmailAddress",
        "Token",
        "AccessToken",
        "RefreshToken",
        "ParticipantToken",
        "Password",
        "Secret",
        "Authorization"
    };

    public static bool IsSensitivePropertyName(string propertyName)
    {
        return SensitivePropertyNames.Contains(propertyName);
    }

    public static string Mask(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        var masked = EmailRegex().Replace(input, RedactedEmail);
        masked = BearerTokenRegex().Replace(masked, $"$1{RedactedToken}");
        masked = TokenKeyValueRegex().Replace(masked, $"$1{RedactedToken}$2");
        return masked;
    }
}
