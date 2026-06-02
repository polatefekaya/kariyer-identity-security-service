using System.Diagnostics;
using Kariyer.Identity.Infrastructure.Telemetry;
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
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.CreateUser", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "create_user");
        activity?.SetTag("supabase.account_type", accountType);

        try
        {
            AdminUserAttributes attributes = new()
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
                throw new InvalidOperationException("Supabase SDK returned a null user or empty ID during admin creation.");

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "create_user"),
                new KeyValuePair<string, object?>("outcome", "success"));

            return Guid.Parse(user.Id);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "create_user"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task BanUserAsync(Guid uid, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.BanUser", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "ban_user");

        try
        {
            AdminUserAttributes attributes = new() { BanDuration = "87600h" };
            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.UpdateUserById(uid.ToString(), attributes);

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "ban_user"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "ban_user"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task UnbanUserAsync(Guid uid, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.UnbanUser", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "unban_user");

        try
        {
            AdminUserAttributes attributes = new() { BanDuration = "none" };
            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.UpdateUserById(uid.ToString(), attributes);

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "unban_user"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "unban_user"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task DeleteUserAsync(Guid uid, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.DeleteUser", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "delete_user");

        try
        {
            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.DeleteUser(uid.ToString());

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "delete_user"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "delete_user"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task SetFrozenStatusAsync(Guid uid, bool isFrozen, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.SetFrozenStatus", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "set_frozen_status");
        activity?.SetTag("supabase.is_frozen", isFrozen.ToString());

        try
        {
            AdminUserAttributes attributes = new()
            {
                UserMetadata = new Dictionary<string, object> { { "is_frozen", isFrozen } }
            };

            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.UpdateUserById(uid.ToString(), attributes);

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "set_frozen_status"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "set_frozen_status"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task UpdateEmailAsync(Guid uid, string email, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.UpdateEmail", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "update_email");

        try
        {
            AdminUserAttributes attributes = new() { Email = email };
            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.UpdateUserById(uid.ToString(), attributes);

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_email"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_email"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task UpdatePhoneAsync(Guid uid, string phone, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.UpdatePhone", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "update_phone");

        try
        {
            AdminUserAttributes attributes = new() { Phone = phone };
            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.UpdateUserById(uid.ToString(), attributes);

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_phone"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_phone"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }

    public async Task UpdatePasswordAsync(Guid uid, string password, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource
            .StartActivity("supabase.admin.UpdatePassword", ActivityKind.Client);
        activity?.SetTag("supabase.operation", "update_password");

        try
        {
            AdminUserAttributes attributes = new() { Password = password };
            IGotrueAdminClient<User> adminAuth = _supabaseClient.AdminAuth(_serviceRoleKey);
            await adminAuth.UpdateUserById(uid.ToString(), attributes);

            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_password"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.SupabaseCallCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_password"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }
}
