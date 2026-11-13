using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Shared.Kernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventsWorld.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Email sender targeting Resend's HTTP API (ADR-0005, database.md §3.6).
/// Sends each outbox message ID as the <c>Idempotency-Key</c> header to prevent duplicate deliveries.
/// </summary>
internal sealed partial class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ResendOptions> _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var apiKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogMissingApiKey(_logger);
            return Result.Failure<EmailSendReceipt>(
                Error.Failure("notifications.resend_missing_key", "Resend API key is not configured."));
        }

        var senderName = _options.Value.SenderName;
        var senderEmail = _options.Value.SenderEmail;
        var from = string.IsNullOrWhiteSpace(senderName)
            ? senderEmail
            : $"{senderName} <{senderEmail}>";

        var requestPayload = new
        {
            from,
            to = new[] { message.To },
            subject = message.Subject,
            html = message.HtmlBody,
            text = message.TextBody
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(requestPayload)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", message.IdempotencyKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogHttpException(_logger, ex, message.IdempotencyKey);
            return Result.Failure<EmailSendReceipt>(
                Error.Failure("notifications.resend_network_error", $"Failed to connect to Resend API: {ex.Message}"));
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LogApiError(_logger, (int)response.StatusCode, errorBody, message.IdempotencyKey);
            return Result.Failure<EmailSendReceipt>(
                Error.Failure("notifications.resend_api_error", $"Resend returned {(int)response.StatusCode}: {errorBody}"));
        }

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("id", out var idElement))
        {
            var providerId = idElement.GetString() ?? string.Empty;
            LogSentSuccess(_logger, providerId, message.IdempotencyKey);
            return Result.Success(new EmailSendReceipt(providerId));
        }

        return Result.Failure<EmailSendReceipt>(
            Error.Failure("notifications.resend_invalid_response", "Resend response did not contain an 'id' field."));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Resend API key is missing from configuration.")]
    private static partial void LogMissingApiKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend request failed for outbox {IdempotencyKey}.")]
    private static partial void LogHttpException(ILogger logger, Exception ex, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend returned status {StatusCode} for outbox {IdempotencyKey}: {ErrorBody}.")]
    private static partial void LogApiError(ILogger logger, int statusCode, string errorBody, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resend accepted email with provider ID {ProviderId} for outbox {IdempotencyKey}.")]
    private static partial void LogSentSuccess(ILogger logger, string providerId, string idempotencyKey);
}
