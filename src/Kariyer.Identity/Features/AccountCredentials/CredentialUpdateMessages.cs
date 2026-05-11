using MassTransit;

namespace Kariyer.Identity.Features.AccountCredentials;

// ── Command published by endpoint service to initiate the saga ───────────────

public record InitiateCredentialSupabaseUpdateCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public Guid ExternalId { get; init; }
    public string CredentialType { get; init; } = string.Empty; // "email" | "phone"
    public string NewValue { get; init; } = string.Empty;
    public string? NewHash { get; init; }
    public string OldValue { get; init; } = string.Empty;
    public string? OldHash { get; init; }
    public string InitiatedBy { get; init; } = string.Empty;
    public string NotificationEmail { get; init; } = string.Empty;
    public string NotificationFullName { get; init; } = string.Empty;
}

// ── Command sent from saga to consumer ───────────────────────────────────────

public record UpdateCredentialInSupabaseCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public Guid ExternalId { get; init; }
    public string CredentialType { get; init; } = string.Empty;
    public string NewValue { get; init; } = string.Empty;
}

// ── Events from consumer back to saga ────────────────────────────────────────

public record CredentialSupabaseUpdatedEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
}

public record CredentialSupabaseUpdateFailedEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
}

// ── Compensation: command from saga to consumer ───────────────────────────────

public record RevertCredentialInDbCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public string CredentialType { get; init; } = string.Empty;
    public string OldValue { get; init; } = string.Empty;
    public string? OldHash { get; init; }
}

// ── Compensation: event from consumer back to saga ────────────────────────────

public record CredentialDbRevertedEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
}
