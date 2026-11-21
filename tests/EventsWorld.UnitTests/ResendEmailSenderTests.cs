using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Modules.Notifications.Infrastructure.Email;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventsWorld.UnitTests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task SendAsync_SendsBearerToken_IdempotencyKey_AndParsesProviderId()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new StubHttpMessageHandler(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"re_msg_987654\"}", Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions
        {
            ApiKey = "re_secret_123",
            SenderEmail = "notificaciones@eventsworld.app",
            SenderName = "Event's World",
            Provider = "Resend"
        });

        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);
        var outboxId = Guid.CreateVersion7().ToString();
        var message = new EmailMessage(outboxId, "user@example.com", "Asunto", "<p>Hola</p>", "Hola");

        var result = await sender.SendAsync(message);

        Assert.True(result.IsSuccess);
        Assert.Equal("re_msg_987654", result.Value.ProviderMessageId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("re_secret_123", capturedRequest.Headers.Authorization?.Parameter);
        Assert.True(capturedRequest.Headers.TryGetValues("Idempotency-Key", out var idempotencyValues));
        Assert.Equal(outboxId, Assert.Single(idempotencyValues));
        Assert.Contains("user@example.com", capturedBody);
    }

    [Fact]
    public async Task SendAsync_When422ClientError_ReturnsPermanentError_WithoutLeakingRecipientEmail()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(
                    "{\"statusCode\":422,\"name\":\"validation_error\",\"message\":\"Invalid 'to' address: private.person@example.com\"}",
                    Encoding.UTF8,
                    "application/json")
            }));

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions { ApiKey = "re_secret_123" });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);

        var result = await sender.SendAsync(new EmailMessage("outbox-1", "private.person@example.com", "Subj", "<p>Hi</p>", "Hi"));

        Assert.True(result.IsFailure);
        var sendError = Assert.IsType<EmailSendError>(result.Error);
        Assert.False(sendError.IsTransient);
        Assert.Contains("validation_error", sendError.Description);
        Assert.DoesNotContain("private.person@example.com", sendError.Description);
    }

    [Fact]
    public async Task SendAsync_When429RateLimited_ReturnsTransientError_WithRetryAfterDelay()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"name\":\"rate_limit_exceeded\"}", Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions { ApiKey = "re_secret_123" });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);

        var result = await sender.SendAsync(new EmailMessage("outbox-2", "user@example.com", "Subj", "<p>Hi</p>", "Hi"));

        Assert.True(result.IsFailure);
        var sendError = Assert.IsType<EmailSendError>(result.Error);
        Assert.True(sendError.IsTransient);
        Assert.Equal(TimeSpan.FromSeconds(120), sendError.RetryAfter);
    }

    [Fact]
    public async Task SendAsync_When503ServerError_ReturnsTransientError()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"name\":\"application_error\"}", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new ResendOptions { ApiKey = "re_secret_123" });
        var sender = new ResendEmailSender(httpClient, options, NullLogger<ResendEmailSender>.Instance);

        var result = await sender.SendAsync(new EmailMessage("outbox-3", "user@example.com", "Subj", "<p>Hi</p>", "Hi"));

        Assert.True(result.IsFailure);
        var sendError = Assert.IsType<EmailSendError>(result.Error);
        Assert.True(sendError.IsTransient);
    }

    [Fact]
    public void ResendOptionsValidator_EnforcesApiKeyInProduction_AndBlocksDevSenderOutsideDevelopment()
    {
        var prodValidator = new ResendOptionsValidator(new StubHostEnvironment(Environments.Production));
        var devValidator = new ResendOptionsValidator(new StubHostEnvironment(Environments.Development));

        // Missing API key when Provider is Resend fails in any environment
        var missingKeyResult = prodValidator.Validate(null, new ResendOptions { Provider = "Resend", ApiKey = "" });
        Assert.True(missingKeyResult.Failed);

        // Development or Smtp provider is blocked in Production
        var devInProdResult = prodValidator.Validate(null, new ResendOptions { Provider = "Development" });
        Assert.True(devInProdResult.Failed);

        var smtpInProdResult = prodValidator.Validate(null, new ResendOptions { Provider = "Smtp" });
        Assert.True(smtpInProdResult.Failed);

        // Development and Smtp providers are allowed in Development
        Assert.True(devValidator.Validate(null, new ResendOptions { Provider = "Development" }).Succeeded);
        Assert.True(devValidator.Validate(null, new ResendOptions { Provider = "Smtp" }).Succeeded);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "EventsWorld.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
