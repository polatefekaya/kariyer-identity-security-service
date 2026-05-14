namespace Kariyer.Identity.Infrastructure.Auth;

internal interface ISupabaseAdminAuthService
{
    Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, string accountType, CancellationToken cancellationToken);
    Task BanUserAsync(Guid uid, CancellationToken cancellationToken);
    Task UnbanUserAsync(Guid uid, CancellationToken cancellationToken);
    Task DeleteUserAsync(Guid uid, CancellationToken cancellationToken);
    Task SetFrozenStatusAsync(Guid uid, bool isFrozen, CancellationToken cancellationToken);
    Task UpdateEmailAsync(Guid uid, string email, CancellationToken cancellationToken);
    Task UpdatePhoneAsync(Guid uid, string phone, CancellationToken cancellationToken);
    Task UpdatePasswordAsync(Guid uid, string password, CancellationToken cancellationToken);
}
