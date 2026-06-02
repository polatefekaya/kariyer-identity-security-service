using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountLifecycle.Saga;

internal sealed class BanUserForDeletionConsumer(
    ISupabaseAdminAuthService supabaseAuth,
    ILogger<BanUserForDeletionConsumer> logger) : IConsumer<BanUserForDeletionCommand>
{
    public async Task Consume(ConsumeContext<BanUserForDeletionCommand> context)
    {
        BanUserForDeletionCommand message = context.Message;

        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("BanUserForDeletion");
        activity?.SetTag("saga.correlation_id", message.CorrelationId.ToString());
        activity?.SetTag("account.uid", message.UserUid);
        activity?.SetTag("account.external_id", message.ExternalId.ToString());

        try
        {
            logger.LogInformation(
                "Banning user {UserUid} (ExternalId: {ExternalId}) in Supabase for deletion grace period.",
                message.UserUid, message.ExternalId);

            await supabaseAuth.BanUserAsync(message.ExternalId, context.CancellationToken);

            activity?.AddEvent(new ActivityEvent("SupabaseUserBanned"));
            logger.LogInformation("Successfully banned user {UserUid} in Supabase.", message.UserUid);

            await context.Publish(new UserBannedForDeletionEvent
            {
                CorrelationId = message.CorrelationId
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to ban user {UserUid} (ExternalId: {ExternalId}) in Supabase.",
                message.UserUid, message.ExternalId);
            throw;
        }
    }
}
