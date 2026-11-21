using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EventsWorld.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Validates <see cref="ResendOptions"/> on application startup so non-development environments
/// fail fast if <c>Resend:ApiKey</c> or <c>Resend:SenderEmail</c> is missing, or if a development-only
/// provider is configured outside <c>Development</c>.
/// </summary>
internal sealed class ResendOptionsValidator(IHostEnvironment environment) : IValidateOptions<ResendOptions>
{
    public ValidateOptionsResult Validate(string? name, ResendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SenderEmail))
        {
            return ValidateOptionsResult.Fail("Resend:SenderEmail must be configured.");
        }

        var provider = options.Provider?.Trim() ?? string.Empty;

        if (string.Equals(provider, "Resend", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return ValidateOptionsResult.Fail(
                    "Resend:ApiKey must be configured when Resend:Provider is 'Resend'.");
            }

            return ValidateOptionsResult.Success;
        }

        if (string.Equals(provider, "Development", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
            {
                return ValidateOptionsResult.Fail(
                    $"Resend:Provider '{provider}' is only permitted in the Development environment.");
            }

            if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(options.SmtpHost) || options.SmtpPort <= 0))
            {
                return ValidateOptionsResult.Fail(
                    "Resend:SmtpHost and a positive Resend:SmtpPort are required when Resend:Provider is 'Smtp'.");
            }

            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"Unsupported Resend:Provider '{options.Provider}'. Expected 'Resend', 'Smtp', or 'Development'.");
    }
}
