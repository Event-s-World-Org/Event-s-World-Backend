using EventsWorld.Api.Logging;
using EventsWorld.Shared.Kernel.Logging;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

// The test writes ad-hoc log calls on purpose, as any code might.
#pragma warning disable CA1848, CA1873

namespace EventsWorld.IntegrationTests;

/// <summary>
/// TS-004: logs never contain an email address, a token or personal data. Uses the same Serilog
/// configuration as the API, writing to a buffer instead of the console.
/// </summary>
public sealed class LoggingTests
{
    private const string Email = "ana.perez@example.com";
    private const string Jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJl";

    [Fact]
    public void Logs_NeverContainEmailsTokensOrPersonalData()
    {
        var output = LogWithApiConfiguration(logger =>
        {
            logger.LogInformation("Registration received for {Email}", Email);
            logger.LogInformation(
                "Registration {@Request}",
                new
                {
                    FullName = "Ana Pérez",
                    PhoneNumber = "+591 70012345",
                    ParticipantToken = "pt_secret_value",
                    Contact = new SensitiveContact("+591 79999999"),
                    EventTitle = "Feria de ciencias",
                });
            logger.LogWarning("Call failed with Authorization: Bearer {Token}", Jwt);
            logger.LogError(
                new InvalidOperationException($"Duplicate registration for {Email} with token={Jwt}"),
                "Registration failed");
        });

        Assert.DoesNotContain(Email, output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJhbGci", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana Pérez", output, StringComparison.Ordinal);
        Assert.DoesNotContain("70012345", output, StringComparison.Ordinal);
        Assert.DoesNotContain("79999999", output, StringComparison.Ordinal);
        Assert.DoesNotContain("pt_secret_value", output, StringComparison.Ordinal);

        // Non-sensitive data stays readable.
        Assert.Contains("Feria de ciencias", output, StringComparison.Ordinal);
        Assert.Contains("Registration failed", output, StringComparison.Ordinal);
    }

    private static string LogWithApiConfiguration(Action<Microsoft.Extensions.Logging.ILogger> write)
    {
        using var output = new StringWriter();
        using (var serilog = new LoggerConfiguration().WithSensitiveDataMasking(output).CreateLogger())
        using (var factory = new SerilogLoggerFactory(serilog))
        {
            write(factory.CreateLogger("Tests"));
        }

        return output.ToString();
    }

    private sealed record SensitiveContact([property: SensitiveData] string Value);
}
