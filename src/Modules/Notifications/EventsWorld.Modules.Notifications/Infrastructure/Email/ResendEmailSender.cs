using System.Net;
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
/// Sends each outbox message ID as the <c>Idempotency-Key</c> header, distinguishes transient errors (429, 5xx, network)
/// from permanent 4xx errors, honors <c>Retry-After</c> on 429, and never logs personal email addresses.
/// </summary>
internal sealed partial class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ResendOptions> _options;
    private readonly ILogger<ResendEmailSender> _logger;
    private readonly TimeProvider _timeProvider;

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailSender> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var apiKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogMissingApiKey(_logger);
            return Result.Failure<EmailSendReceipt>(
                EmailSendError.Permanent("notifications.resend_missing_key", "Resend API key is not configured."));
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
                EmailSendError.Transient("notifications.resend_network_error", "Failed to connect to Resend API."));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var errorCodeName = ExtractSafeErrorName(errorBody);
                var statusCode = (int)response.StatusCode;

                LogApiError(_logger, statusCode, errorCodeName, message.IdempotencyKey);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = ExtractRetryAfter(response.Headers.RetryAfter);
                    return Result.Failure<EmailSendReceipt>(
                        EmailSendError.Transient(
                            "notifications.resend_rate_limited",
                            $"Resend rate limit exceeded (429: {errorCodeName}).",
                            retryAfter));
                }

                var isTransient = statusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;
                var error = isTransient
                    ? EmailSendError.Transient(
                        "notifications.resend_server_error",
                        $"Resend returned transient status {statusCode} ({errorCodeName}).")
                    : EmailSendError.Permanent(
                        "notifications.resend_rejected",
                        $"Resend rejected request with status {statusCode} ({errorCodeName}).");

                return Result.Failure<EmailSendReceipt>(error);
            }

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (document.RootElement.TryGetProperty("id", out var idElement))
            {
                var providerId = idElement.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(providerId))
                {
                    LogSentSuccess(_logger, providerId, message.IdempotencyKey);
                    return Result.Success(new EmailSendReceipt(providerId));
                }
            }

            return Result.Failure<EmailSendReceipt>(
                EmailSendError.Transient("notifications.resend_invalid_response", "Resend response did not contain a valid 'id' field."));
        }
    }

    private TimeSpan? ExtractRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var remaining = date - _timeProvider.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }
        }

        return null;
    }

    private static string ExtractSafeErrorName(string? errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return "unknown_error";
        }

        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore non-JSON body and return safe identifier below
        }

        return "api_error";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Resend API key is missing from configuration.")]
    private static partial void LogMissingApiKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend request failed for outbox {IdempotencyKey}.")]
    private static partial void LogHttpException(ILogger logger, Exception ex, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend returned status {StatusCode} ({ErrorName}) for outbox {IdempotencyKey}.")]
    private static partial void LogApiError(ILogger logger, int statusCode, string errorName, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resend accepted email with provider ID {ProviderId} for outbox {IdempotencyKey}.")]
    private static partial void LogSentSuccess(ILogger logger, string providerId, string idempotencyKey);
}
