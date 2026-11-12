using System.Reflection;
using System.Text.RegularExpressions;
using EventsWorld.Shared.Kernel.Logging;

namespace EventsWorld.Shared.Infrastructure.Logging;

/// <summary>
/// Removes personal data and secrets from log output (backend.md §5, NFR-04): values of sensitive
/// properties, and emails, bearer tokens, JWTs and <c>token=...</c> pairs found inside any text.
/// </summary>
public static partial class SensitiveDataMasker
{
    public const string Redacted = "[REDACTED]";
    public const string RedactedEmail = "***@***.***";
    public const string RedactedToken = "[REDACTED_TOKEN]";

    /// <summary>
    /// A property is sensitive when its name contains one of these fragments (case-insensitive),
    /// so <c>Email</c>, <c>ContactEmail</c>, <c>AccessToken</c> and <c>PhoneNumber</c> all match.
    /// </summary>
    private static readonly string[] SensitiveNameFragments =
    [
        "email", "token", "password", "secret", "authorization", "apikey", "cookie",
        "phone", "fullname", "firstname", "lastname", "document", "birth", "address", "answers",
    ];

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)\b(bearer\s+)[A-Za-z0-9\-._~+/]+=*")]
    private static partial Regex BearerTokenRegex();

    /// <summary>A JWT anywhere in the text: three base64url parts, the first one starts with "eyJ".</summary>
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)(""?(?:access_token|refresh_token|token|password|secret|authorization|apikey)""?\s*[:=]\s*""?)[^""\s,;&]+(""?)")]
    private static partial Regex TokenKeyValueRegex();

    public static bool IsSensitivePropertyName(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return SensitiveNameFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSensitive(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return IsSensitivePropertyName(property.Name)
            || property.IsDefined(typeof(SensitiveDataAttribute), inherit: true);
    }

    /// <summary>The value written instead of a sensitive property.</summary>
    public static string RedactedValueFor(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return propertyName.Contains("email", StringComparison.OrdinalIgnoreCase) ? RedactedEmail : Redacted;
    }

    public static string Mask(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        var masked = EmailRegex().Replace(input, RedactedEmail);
        masked = BearerTokenRegex().Replace(masked, $"$1{RedactedToken}");
        masked = JwtRegex().Replace(masked, RedactedToken);
        masked = TokenKeyValueRegex().Replace(masked, $"$1{RedactedToken}$2");
        return masked;
    }
}
