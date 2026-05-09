using MassTransit;

namespace Kariyer.Identity.Features.AccountLifecycle;

// ── Commands sent TO the saga from endpoints / sweeper ──────────────────────

public record RequestAccountDeletionCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public Guid ExternalId { get; init; }
    public string InitiatedBy { get; init; } = string.Empty;
    public string? InitiatedByUid { get; init; }
}

public record CancelAccountDeletionCommand
{
    public string UserUid { get; init; } = string.Empty;
    public string CancelledByUid { get; init; } = string.Empty;
}

public record ExecuteAccountDeletionCommand
{
    public string UserUid { get; init; } = string.Empty;
}

// ── Commands sent FROM the saga to consumers ─────────────────────────────────

public record BanUserForDeletionCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public Guid ExternalId { get; init; }
}

public record UnbanUserCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public Guid ExternalId { get; init; }
}

public record DeleteUserPermanentlyCommand : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public Guid ExternalId { get; init; }
}

// ── Events sent FROM consumers back to the saga ──────────────────────────────

public record UserBannedForDeletionEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
}

public record UserUnbannedEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}

public record UserPermanentlyDeletedEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public string UserUid { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
