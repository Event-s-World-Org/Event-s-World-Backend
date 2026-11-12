using EventsWorld.Shared.Infrastructure.Logging;

namespace EventsWorld.UnitTests;

public sealed class SensitiveDataMaskingTests
{
    [Theory]
    [InlineData("user@example.com", SensitiveDataMasker.RedactedEmail)]
    [InlineData("Contact us at support@eventsworld.com today", "Contact us at ***@***.*** today")]
    [InlineData("Multiple: test.user+label@domain.co.uk and admin@org.bo", "Multiple: ***@***.*** and ***@***.***")]
    public void Mask_ShouldMaskEmailAddresses(string input, string expected)
    {
        Assert.Equal(expected, SensitiveDataMasker.Mask(input));
    }

    [Theory]
    [InlineData("Bearer abc123def456xyz", "Bearer [REDACTED_TOKEN]")]
    [InlineData("bearer secret_jwt_token_payload==", "bearer [REDACTED_TOKEN]")]
    [InlineData("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.sig-part", "jwt [REDACTED_TOKEN]")]
    [InlineData("access_token=abc123&state=1", "access_token=[REDACTED_TOKEN]&state=1")]
    [InlineData("{\"password\": \"hunter2\"}", "{\"password\": \"[REDACTED_TOKEN]\"}")]
    public void Mask_ShouldMaskTokensAndSecrets(string input, string expected)
    {
        Assert.Equal(expected, SensitiveDataMasker.Mask(input));
    }

    [Theory]
    [InlineData("Email", true)]
    [InlineData("ContactEmail", true)]
    [InlineData("AccessToken", true)]
    [InlineData("participantToken", true)]
    [InlineData("Password", true)]
    [InlineData("Authorization", true)]
    [InlineData("PhoneNumber", true)]
    [InlineData("FullName", true)]
    [InlineData("DocumentNumber", true)]
    [InlineData("Answers", true)]
    [InlineData("EventTitle", false)]
    [InlineData("Description", false)]
    [InlineData("Amount", false)]
    public void IsSensitivePropertyName_ShouldMatchByNameFragment(string propertyName, bool expected)
    {
        Assert.Equal(expected, SensitiveDataMasker.IsSensitivePropertyName(propertyName));
    }
}
