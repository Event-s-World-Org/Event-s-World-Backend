namespace EventsWorld.Modules.Notifications.Contracts;

/// <summary>
/// Canonical template identifiers for transactional emails (FR-07.1, database.md §3.6).
/// </summary>
public static class EmailTemplates
{
    public const string RegistrationReceived = "registration_received";
    public const string PaymentApproved = "payment_approved";
    public const string PaymentObserved = "payment_observed";
    public const string PaymentRejected = "payment_rejected";
    public const string RegistrationRejectedFinal = "registration_rejected_final";
    public const string ParticipantDataEdited = "participant_data_edited";
    public const string PaymentQrExpired = "payment_qr_expired";
    public const string EventSuspended = "event_suspended";
    public const string EventReactivated = "event_reactivated";
    public const string PersonalLinkRecovery = "personal_link_recovery";
}
