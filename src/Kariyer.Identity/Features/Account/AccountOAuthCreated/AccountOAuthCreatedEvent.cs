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

        logger.LogInformation("Processing OAuth account metadata update for User ID: {UserId}. Provider: {Provider}", 
            message.UserId, 
            message.Provider);

        if (string.Equals(message.AccountType, "employee", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string serviceRoleKey = configuration["ExternalProvider:ServiceRoleKey"] 
                    ?? throw new ArgumentNullException("CRITICAL: ExternalProvider:ServiceRoleKey is missing from configuration.");
                
                IGotrueAdminClient<User> adminAuthClient = supabaseClient.AdminAuth(serviceRoleKey);
                AdminUserAttributes updateAttributes = new()
                {
                    UserMetadata = new Dictionary<string, object>
                    {
                        { "account_type", "employee" }
                    }
                };

                await adminAuthClient.UpdateUserById(message.UserId.ToString(), updateAttributes);

                logger.LogInformation("Successfully backfilled user_metadata for User ID: {UserId} via Admin API.", message.UserId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Supabase Admin API failed to update user_metadata for User ID: {UserId}", message.UserId);
                
                throw;
            }
        }
        else
        {
            logger.LogInformation("Account type '{AccountType}' does not require forced metadata backfilling. Skipping.", message.AccountType);
        }
    }
}