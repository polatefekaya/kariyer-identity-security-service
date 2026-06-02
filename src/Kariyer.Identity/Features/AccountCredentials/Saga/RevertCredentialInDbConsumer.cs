using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountCredentials.Saga;

internal sealed class RevertCredentialInDbConsumer(
    IdentityDbContext dbContext,
    ILogger<RevertCredentialInDbConsumer> logger) : IConsumer<RevertCredentialInDbCommand>
{
    public async Task Consume(ConsumeContext<RevertCredentialInDbCommand> context)
    {
        RevertCredentialInDbCommand message = context.Message;

        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("RevertCredentialInDb");
        activity?.SetTag("saga.correlation_id", message.CorrelationId.ToString());
        activity?.SetTag("account.uid", message.UserUid);
        activity?.SetTag("credential.type", message.CredentialType);

        try
        {
            if (message.UserType == "employee")
            {
                LegacyEmployee? employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == message.UserUid, context.CancellationToken);

                if (employee is null)
                    logger.LogWarning("Employee {UserUid} not found during credential revert for saga {CorrelationId}. Skipping.",
                        message.UserUid, message.CorrelationId);
                else if (message.CredentialType == "email")
                    employee.RevertEmail(message.OldValue, message.OldHash);
                else
                    employee.RevertPhone(message.OldValue, message.OldHash);
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == message.UserUid, context.CancellationToken);

                if (company is null)
                    logger.LogWarning("Company {UserUid} not found during credential revert for saga {CorrelationId}. Skipping.",
                        message.UserUid, message.CorrelationId);
                else if (message.CredentialType == "email")
                    company.RevertEmail(message.OldValue, message.OldHash);
                else
                    company.RevertPhone(message.OldValue, message.OldHash);
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);

            activity?.AddEvent(new ActivityEvent("CredentialRevertedInDb"));
            logger.LogInformation("Credential ({Type}) reverted in DB for {UserUid} (saga {CorrelationId}).",
                message.CredentialType, message.UserUid, message.CorrelationId);

            await context.Publish(new CredentialDbRevertedEvent
            {
                CorrelationId = message.CorrelationId
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to revert credential in DB for {UserUid} (saga {CorrelationId}).",
                message.UserUid, message.CorrelationId);
            throw;
        }
    }
}
