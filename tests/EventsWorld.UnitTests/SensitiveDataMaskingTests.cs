using EventsWorld.Shared.Kernel.Logging;

namespace EventsWorld.UnitTests;

public sealed class SensitiveDataMaskingTests
{
    [Theory]
    [InlineData("user@example.com", SensitiveDataMasker.RedactedEmail)]
    [InlineData("Contact us at support@eventsworld.com today", "Contact us at ***@***.*** today")]
    [InlineData("Multiple: test.user+label@domain.co.uk and admin@org.bo", "Multiple: ***@***.*** and ***@***.***")]
    public void Mask_ShouldMaskEmailAddresses(string input, string expected)
    {
        var result = SensitiveDataMasker.Mask(input);

        Assert.Equal(expected, result);
        Assert.DoesNotContain("@example.com", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@eventsworld.com", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Bearer abc123def456xyz", "Bearer [REDACTED_TOKEN]")]
    [InlineData("bearer secret_jwt_token_payload==", "bearer [REDACTED_TOKEN]")]
    [InlineData("Header: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.t-IDcbeZrQzzz", "Header: Bearer [REDACTED_TOKEN]")]
    public void Mask_ShouldMaskBearerTokens(string input, string expected)
    {
        var result = SensitiveDataMasker.Mask(input);

        Assert.Equal(expected, result);
        Assert.DoesNotContain("abc123def456xyz", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJhbGci", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Email", true)]
    [InlineData("emailaddress", true)]
    [InlineData("token", true)]
    [InlineData("AccessToken", true)]
    [InlineData("participantToken", true)]
    [InlineData("Password", true)]
    [InlineData("Authorization", true)]
    [InlineData("EventTitle", false)]
    [InlineData("Description", false)]
    [InlineData("Amount", false)]
    public void IsSensitivePropertyName_ShouldIdentifySensitiveNames(string propertyName, bool expected)
    {
        var isSensitive = SensitiveDataMasker.IsSensitivePropertyName(propertyName);
        Assert.Equal(expected, isSensitive);
    }
}
