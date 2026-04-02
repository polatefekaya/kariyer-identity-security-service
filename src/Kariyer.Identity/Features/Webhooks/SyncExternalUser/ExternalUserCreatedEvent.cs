using System;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public record ExternalUserCreatedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
}