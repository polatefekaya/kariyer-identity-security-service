using MassTransit;

namespace Kariyer.Identity.Domain.Entities;

public class CredentialUpdateSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public Guid ExternalId { get; set; }

    public string CredentialType { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string? NewHash { get; set; }
    public string OldValue { get; set; } = string.Empty;
    public string? OldHash { get; set; }

    public string InitiatedBy { get; set; } = string.Empty;
    public string NotificationEmail { get; set; } = string.Empty;
    public string NotificationFullName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
