using System.Diagnostics;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Kariyer.Identity.Features.Account.AccountOAuthCreated;

public class AccountOAuthCreatedConsumer(
    Supabase.Client supabaseClient,
    IConfiguration configuration,
    ILogger<AccountOAuthCreatedConsumer> logger) : IConsumer<AccountOAuthCreatedEvent>
{
    public async Task Consume(ConsumeContext<AccountOAuthCreatedEvent> context)
    {
        AccountOAuthCreatedEvent message = context.Message;

        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("Supabase.UpdateUserMetadata");
        
        activity?.SetTag("user.id", message.UserId.ToString());
        activity?.SetTag("oauth.provider", message.Provider);
        activity?.SetTag("oauth.account_type", message.AccountType);

        if (message.UserId == Guid.Empty)
        {
            logger.LogWarning("Message consumed with empty UserId. Discarding payload.");
            activity?.SetStatus(ActivityStatusCode.Error, "Empty UserId");
            return;
        }

        logger.LogInformation("Processing OAuth account metadata update for User ID: {UserId}. Provider: {Provider}", 
            message.UserId, 
            message.Provider);

        if (!string.Equals(message.AccountType, "employee", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Account type '{AccountType}' does not require forced metadata backfilling. Skipping.", message.AccountType);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return;
        }

        string? serviceRoleKey = configuration["ExternalProvider:ServiceRoleKey"];
        if (string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            InvalidOperationException configEx = new("CRITICAL: ExternalProvider:ServiceRoleKey is missing from configuration.");
            activity?.AddException(configEx);
            activity?.SetStatus(ActivityStatusCode.Error, configEx.Message);
            throw configEx;
        }

        try
        {
            IGotrueAdminClient<User> adminAuthClient = supabaseClient.AdminAuth(serviceRoleKey);
            
            AdminUserAttributes updateAttributes = new()
            {
                UserMetadata = new Dictionary<string, object>
                {
                    { "account_type", "employee" }
                }
            };

            await adminAuthClient.UpdateUserById(message.UserId.ToString(), updateAttributes);

            IdentityDiagnostics.OAuthMetadataSyncCounter.Add(1, 
                new KeyValuePair<string, object?>("status", "success"),
                new KeyValuePair<string, object?>("provider", message.Provider));

            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.LogInformation("Successfully backfilled user_metadata for User ID: {UserId} via Admin API.", message.UserId);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            IdentityDiagnostics.OAuthMetadataSyncCounter.Add(1, 
                new KeyValuePair<string, object?>("status", "failure"),
                new KeyValuePair<string, object?>("provider", message.Provider));

            logger.LogError(ex, "FATAL: Supabase Admin API failed to update user_metadata for User ID: {UserId}", message.UserId);
            
            throw; 
        }
    }
}