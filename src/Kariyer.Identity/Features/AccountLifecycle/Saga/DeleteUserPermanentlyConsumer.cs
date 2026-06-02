using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountLifecycle.Saga;

internal sealed class DeleteUserPermanentlyConsumer(
    ISupabaseAdminAuthService supabaseAuth,
    IdentityDbContext dbContext,
    ILogger<DeleteUserPermanentlyConsumer> logger) : IConsumer<DeleteUserPermanentlyCommand>
{
    public async Task Consume(ConsumeContext<DeleteUserPermanentlyCommand> context)
    {
        DeleteUserPermanentlyCommand message = context.Message;

        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("DeleteUserPermanently");
        activity?.SetTag("saga.correlation_id", message.CorrelationId.ToString());
        activity?.SetTag("account.uid", message.UserUid);
        activity?.SetTag("account.type", message.UserType);
        activity?.SetTag("account.external_id", message.ExternalId.ToString());

        try
        {
            // Deterministic, traceable suffix derived from the Supabase UUID
            string suffix = message.ExternalId.ToString("N")[..8];

            string originalEmail;
            string fullName;

            if (message.UserType == "employee")
            {
                LegacyEmployee employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == message.UserUid, context.CancellationToken)
                    ?? throw new InvalidOperationException($"Employee {message.UserUid} not found for permanent deletion.");

                if (employee.PermaDeleted)
                {
                    logger.LogWarning("Employee {UserUid} is already permanently deleted. Skipping (idempotent).", message.UserUid);
                    await PublishCompletionEvents(context, message, employee.Email, string.Empty);
                    return;
                }

                originalEmail = employee.Email;
                fullName = $"{employee.Name} {employee.Surname}".Trim();

                logger.LogInformation("Deleting employee {UserUid} from Supabase.", message.UserUid);
                await DeleteFromSupabase(message.ExternalId, message.UserUid, context.CancellationToken);
                activity?.AddEvent(new ActivityEvent("SupabaseUserDeleted"));

                employee.AnonymizeForDeletion(suffix);
            }
            else
            {
                LegacyCompany company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == message.UserUid, context.CancellationToken)
                    ?? throw new InvalidOperationException($"Company {message.UserUid} not found for permanent deletion.");

                if (company.PermaDeleted)
                {
                    logger.LogWarning("Company {UserUid} is already permanently deleted. Skipping (idempotent).", message.UserUid);
                    await PublishCompletionEvents(context, message, company.Email, string.Empty);
                    return;
                }

                originalEmail = company.Email;
                fullName = company.CompanyName ?? company.AuthorizedName ?? string.Empty;

                logger.LogInformation("Deleting company {UserUid} from Supabase.", message.UserUid);
                await DeleteFromSupabase(message.ExternalId, message.UserUid, context.CancellationToken);
                activity?.AddEvent(new ActivityEvent("SupabaseUserDeleted"));

                company.AnonymizeForDeletion(suffix);
            }

            // Both events go through the outbox – atomic with the DB anonymization
            await context.Publish(new UserPermanentlyDeletedEvent
            {
                CorrelationId = message.CorrelationId,
                UserUid = message.UserUid,
                Email = originalEmail,
                FullName = fullName
            }, context.CancellationToken);

            await context.Publish(new AccountDeletedEvent
            {
                MessageId = Guid.NewGuid().ToString(),
                Uid = message.UserUid,
                Email = originalEmail,
                FullName = fullName
            }, context.CancellationToken);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation("Successfully permanently deleted user {UserUid}.", message.UserUid);
            IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "user_permanently_deleted"),
                new KeyValuePair<string, object?>("user_type", message.UserType));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to permanently delete user {UserUid}.", message.UserUid);
            throw;
        }
    }

    private async Task DeleteFromSupabase(Guid externalId, string userUid, CancellationToken ct)
    {
        try
        {
            await supabaseAuth.DeleteUserAsync(externalId, ct);
        }
        catch (Exception ex) when (IsUserNotFoundError(ex))
        {
            // Already deleted from Supabase – treat as success (idempotent)
            logger.LogWarning("User {UserUid} (ExternalId: {ExternalId}) not found in Supabase during deletion. Treating as already deleted.",
                userUid, externalId);
        }
    }

    private static bool IsUserNotFoundError(Exception ex) =>
        ex.Message.Contains("User not found", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase);

    private static async Task PublishCompletionEvents(
        ConsumeContext context,
        DeleteUserPermanentlyCommand message,
        string email,
        string fullName)
    {
        await context.Publish(new UserPermanentlyDeletedEvent
        {
            CorrelationId = message.CorrelationId,
            UserUid = message.UserUid,
            Email = email,
            FullName = fullName
        }, context.CancellationToken);
    }
}
