using MassTransit;

namespace Kariyer.Identity.Domain.Entities;

public class AccountDeletionSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public Guid ExternalId { get; set; }

    public string InitiatedBy { get; set; } = string.Empty;
    public string? InitiatedByUid { get; set; }

    public DateTimeOffset? GracePeriodEndsAt { get; set; }
    public DateTimeOffset? SupabaseBannedAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledByUid { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
