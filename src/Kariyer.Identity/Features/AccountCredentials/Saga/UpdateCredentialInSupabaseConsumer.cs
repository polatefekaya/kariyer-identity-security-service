using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountCredentials.Saga;

internal sealed class UpdateCredentialInSupabaseConsumer(
    ISupabaseAdminAuthService supabaseAuth,
    ILogger<UpdateCredentialInSupabaseConsumer> logger) : IConsumer<UpdateCredentialInSupabaseCommand>
{
    private const int MaxAttempts = 3;

    public async Task Consume(ConsumeContext<UpdateCredentialInSupabaseCommand> context)
    {
        UpdateCredentialInSupabaseCommand message = context.Message;

        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateCredentialInSupabase");
        activity?.SetTag("saga.correlation_id", message.CorrelationId.ToString());
        activity?.SetTag("account.external_id", message.ExternalId.ToString());
        activity?.SetTag("credential.type", message.CredentialType);

        Exception? lastEx = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (message.CredentialType == "email")
                    await supabaseAuth.UpdateEmailAsync(message.ExternalId, message.NewValue, context.CancellationToken);
                else
                    await supabaseAuth.UpdatePhoneAsync(message.ExternalId, message.NewValue, context.CancellationToken);

                activity?.AddEvent(new ActivityEvent("CredentialUpdatedInSupabase"));
                logger.LogInformation(
                    "Credential ({Type}) updated in Supabase for ExternalId={ExternalId} (saga {CorrelationId}).",
                    message.CredentialType, message.ExternalId, message.CorrelationId);

                await context.Publish(new CredentialSupabaseUpdatedEvent
                {
                    CorrelationId = message.CorrelationId
                }, context.CancellationToken);

                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                logger.LogWarning(ex,
                    "Supabase credential update attempt {Attempt}/{Max} failed for CorrelationId={CorrelationId}.",
                    attempt, MaxAttempts, message.CorrelationId);

                if (attempt < MaxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), context.CancellationToken);
            }
        }

        if (lastEx is not null) activity?.AddException(lastEx);
        activity?.SetStatus(ActivityStatusCode.Error, lastEx?.Message);
        logger.LogError(lastEx,
            "Supabase credential update failed after {MaxAttempts} attempts for CorrelationId={CorrelationId}. Triggering DB compensation.",
            MaxAttempts, message.CorrelationId);

        await context.Publish(new CredentialSupabaseUpdateFailedEvent
        {
            CorrelationId = message.CorrelationId
        }, context.CancellationToken);
    }
}
