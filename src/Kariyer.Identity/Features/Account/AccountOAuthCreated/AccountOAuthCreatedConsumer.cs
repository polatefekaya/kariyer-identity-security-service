namespace Kariyer.Identity.Features.Account.AccountOAuthCreated;

public record AccountOAuthCreatedEvent
{
    public Guid UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
}