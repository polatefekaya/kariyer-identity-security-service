namespace Kariyer.Identity.Infrastructure.Auth;

internal interface ISupabaseAdminAuthService
{
    Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, string accountType, CancellationToken cancellationToken);
    Task BanUserAsync(Guid uid, CancellationToken cancellationToken);
    Task UnbanUserAsync(Guid uid, CancellationToken cancellationToken);
    Task DeleteUserAsync(Guid uid, CancellationToken cancellationToken);
}
