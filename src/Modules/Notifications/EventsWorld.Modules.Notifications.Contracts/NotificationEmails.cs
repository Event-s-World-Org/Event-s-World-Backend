using System.Text.Json.Serialization;

namespace EventsWorld.Modules.Notifications.Contracts;

/// <summary>
/// Base contract for every transactional email staged into the outbox (FR-07.1, ADR-0003 §5).
/// </summary>
public abstract record NotificationEmail(
    [property: JsonPropertyName("template")] string Template,
    [property: JsonPropertyName("to_email")] string ToEmail);

/// <summary>FR-07.1: Registration received with registration code and personal link.</summary>
public sealed record RegistrationReceivedEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("personal_link")] string PersonalLink)
    : NotificationEmail(EmailTemplates.RegistrationReceived, ToEmail);

/// <summary>FR-07.1: Payment approved with registration code and personal link.</summary>
public sealed record PaymentApprovedEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("personal_link")] string PersonalLink)
    : NotificationEmail(EmailTemplates.PaymentApproved, ToEmail);

/// <summary>FR-07.1: Payment voucher observed with reason, optional deadline, and personal link.</summary>
public sealed record PaymentObservedEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("personal_link")] string PersonalLink,
    [property: JsonPropertyName("deadline")] string? Deadline = null)
    : NotificationEmail(EmailTemplates.PaymentObserved, ToEmail);

/// <summary>FR-07.1: Payment voucher rejected with reason, optional deadline to resubmit, and personal link.</summary>
public sealed record PaymentRejectedEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("personal_link")] string PersonalLink,
    [property: JsonPropertyName("deadline")] string? Deadline = null)
    : NotificationEmail(EmailTemplates.PaymentRejected, ToEmail);

/// <summary>FR-07.1 / FR-03.5: Registration rejected for good after the registration deadline.</summary>
public sealed record RegistrationRejectedFinalEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("personal_link")] string PersonalLink)
    : NotificationEmail(EmailTemplates.RegistrationRejectedFinal, ToEmail);

/// <summary>FR-07.1 / FR-03.7: Participant data edited by the organizer with reason and personal link.</summary>
public sealed record ParticipantDataEditedEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("personal_link")] string PersonalLink)
    : NotificationEmail(EmailTemplates.ParticipantDataEdited, ToEmail);

/// <summary>FR-07.1 / FR-02.2: Payment QR expired and paid registrations paused (sent to the organizer).</summary>
public sealed record PaymentQrExpiredEmail(
    string ToEmail,
    [property: JsonPropertyName("organizer_name")] string OrganizerName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("expired_date")] string ExpiredDate,
    [property: JsonPropertyName("manage_event_link")] string ManageEventLink)
    : NotificationEmail(EmailTemplates.PaymentQrExpired, ToEmail);

/// <summary>FR-07.1 / FR-06.2: Event suspended by the Super Admin (sent to the organizer).</summary>
public sealed record EventSuspendedEmail(
    string ToEmail,
    [property: JsonPropertyName("organizer_name")] string OrganizerName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("manage_event_link")] string ManageEventLink)
    : NotificationEmail(EmailTemplates.EventSuspended, ToEmail);

/// <summary>FR-07.1 / FR-06.2: Event reactivated by the Super Admin (sent to the organizer).</summary>
public sealed record EventReactivatedEmail(
    string ToEmail,
    [property: JsonPropertyName("organizer_name")] string OrganizerName,
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("manage_event_link")] string ManageEventLink)
    : NotificationEmail(EmailTemplates.EventReactivated, ToEmail);

/// <summary>Single registration entry inside a personal link recovery email.</summary>
public sealed record RecoveredRegistrationItem(
    [property: JsonPropertyName("event_title")] string EventTitle,
    [property: JsonPropertyName("registration_code")] string RegistrationCode,
    [property: JsonPropertyName("personal_link")] string PersonalLink);

/// <summary>FR-07.1 / FR-05.2: Personal link recovery email listing active registrations.</summary>
public sealed record PersonalLinkRecoveryEmail(
    string ToEmail,
    [property: JsonPropertyName("participant_name")] string ParticipantName,
    [property: JsonPropertyName("registrations")] IReadOnlyList<RecoveredRegistrationItem> Registrations)
    : NotificationEmail(EmailTemplates.PersonalLinkRecovery, ToEmail);
