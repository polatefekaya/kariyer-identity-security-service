using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountLifecycle.Saga;

internal sealed class UnbanUserConsumer(
    ISupabaseAdminAuthService supabaseAuth,
    IdentityDbContext dbContext,
    ILogger<UnbanUserConsumer> logger) : IConsumer<UnbanUserCommand>
{
    public async Task Consume(ConsumeContext<UnbanUserCommand> context)
    {
        UnbanUserCommand message = context.Message;

        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UnbanUserForDeletionCancel");
        activity?.SetTag("saga.correlation_id", message.CorrelationId.ToString());
        activity?.SetTag("account.uid", message.UserUid);
        activity?.SetTag("account.external_id", message.ExternalId.ToString());

        try
        {
            logger.LogInformation(
                "Unbanning user {UserUid} (ExternalId: {ExternalId}) in Supabase after deletion cancellation.",
                message.UserUid, message.ExternalId);

            await supabaseAuth.UnbanUserAsync(message.ExternalId, context.CancellationToken);

            activity?.AddEvent(new ActivityEvent("SupabaseUserUnbanned"));
            logger.LogInformation("Successfully unbanned user {UserUid} in Supabase.", message.UserUid);

            string email = string.Empty;
            string fullName = string.Empty;

            LegacyEmployee? employee = await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Uid == message.UserUid, context.CancellationToken);

            if (employee is not null)
            {
                email = employee.Email;
                fullName = $"{employee.Name} {employee.Surname}".Trim();
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == message.UserUid, context.CancellationToken);
                if (company is not null)
                {
                    email = company.Email;
                    fullName = company.CompanyName ?? company.AuthorizedName ?? string.Empty;
                }
            }

            if (string.IsNullOrEmpty(email))
                logger.LogWarning("Could not resolve email for user {UserUid} during unban — cancellation email will not be sent.", message.UserUid);

            await context.Publish(new UserUnbannedEvent
            {
                CorrelationId = message.CorrelationId,
                UserUid = message.UserUid,
                Email = email,
                FullName = fullName
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to unban user {UserUid} (ExternalId: {ExternalId}) in Supabase.",
                message.UserUid, message.ExternalId);
            throw;
        }
    }
}
