using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Kariyer.Identity.Infrastructure.Auth;

internal sealed class SupabaseAdminAuthService : ISupabaseAdminAuthService
{
    private readonly Supabase.Client _supabaseClient;
    private readonly string _serviceRoleKey;

    public SupabaseAdminAuthService(Supabase.Client supabaseClient, IConfiguration configuration)
    {
        _supabaseClient = supabaseClient;
        _serviceRoleKey = configuration["ExternalProvider:ServiceRoleKey"]
            ?? throw new ArgumentNullException("ExternalProvider:ServiceRoleKey missing in config.");
    }

    public async Task<Guid> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string accountType,
        CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new ()
        {
            Email = email,
            Password = password,
            EmailConfirm = true,
            PhoneConfirm = false,
            UserMetadata = new Dictionary<string, object>
            {
                { "first_name", firstName },
                { "last_name", lastName },
                { "account_type", accountType },
                { "email_verified", true }
            }
        };

        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);

        User? user = await adminAuth.CreateUser(attributes);

        if (user == null || string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException("Supabase SDK returned a null user or empty ID during admin creation.");
        }

        return Guid.Parse(user.Id);
    }

    public async Task BanUserAsync(Guid uid, CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new ()
        {
            BanDuration = "87600h"
        };

        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.UpdateUserById(uid.ToString(), attributes);
    }

    public async Task UnbanUserAsync(Guid uid, CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new AdminUserAttributes
        {
            BanDuration = "none"
        };

        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.UpdateUserById(uid.ToString(), attributes);
    }

    public async Task DeleteUserAsync(Guid uid, CancellationToken cancellationToken)
    {
        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.DeleteUser(uid.ToString());
    }

    public async Task SetFrozenStatusAsync(Guid uid, bool isFrozen, CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new()
        {
            UserMetadata = new Dictionary<string, object>
            {
                { "is_frozen", isFrozen }
            }
        };

        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.UpdateUserById(uid.ToString(), attributes);
    }

    public async Task UpdateEmailAsync(Guid uid, string email, CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new() { Email = email };
        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.UpdateUserById(uid.ToString(), attributes);
    }

    public async Task UpdatePhoneAsync(Guid uid, string phone, CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new() { Phone = phone };
        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.UpdateUserById(uid.ToString(), attributes);
    }

    public async Task UpdatePasswordAsync(Guid uid, string password, CancellationToken cancellationToken)
    {
        AdminUserAttributes attributes = new() { Password = password };
        IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
        await adminAuth.UpdateUserById(uid.ToString(), attributes);
    }
}