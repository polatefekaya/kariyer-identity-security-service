namespace Kariyer.Identity.Features.AccountLifecycle;

internal record DeletionStatusDto(
    string UserUid,
    string UserType,
    string CurrentState,
    string InitiatedBy,
    string? InitiatedByUid,
    DateTimeOffset? GracePeriodEndsAt,
    DateTimeOffset? SupabaseBannedAt,
    DateTimeOffset? ExecutedAt,
    DateTimeOffset? CancelledAt,
    string? CancelledByUid,
    DateTimeOffset CreatedAt
);
