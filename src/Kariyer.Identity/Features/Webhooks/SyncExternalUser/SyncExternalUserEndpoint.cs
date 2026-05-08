using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public static class SyncExternalUserEndpoint
{
    public static void MapSyncSupabaseUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/supabase/user-created", async (
            [FromBody] DatabaseWebhookPayload payload,
            IdentityDbContext dbContext,
            IPublishEndpoint publishEndpoint,
            ILogger<SupabaseDatabaseWebhookFilter> logger,
            CancellationToken cancellationToken) =>
        {
            using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("Supabase.DatabaseWebhook.UserCreated");
            
            string eventType = payload.Type ?? "unknown";
            string tableName = payload.Table ?? "unknown";
            
            activity?.SetTag("webhook.type", eventType);
            activity?.SetTag("webhook.table", tableName);

            logger.LogInformation("Database Webhook received. Type: '{Type}', Table: '{Table}'", eventType, tableName);

            if (!string.Equals(eventType, "INSERT", StringComparison.OrdinalIgnoreCase) || 
                !string.Equals(tableName, "users", StringComparison.OrdinalIgnoreCase)) 
            {
                logger.LogWarning("Webhook aborted. Ignoring event '{Type}' on table '{Table}' to prevent processing errors.", eventType, tableName);
                
                activity?.SetStatus(ActivityStatusCode.Ok, "Ignored event type or table");
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "ignored_event_type"));
                
                return Results.Ok();
            }

            if (payload.Record == null)
            {
                logger.LogError("Webhook payload contained no Record data.");
                
                activity?.SetStatus(ActivityStatusCode.Error, "Missing Record data");
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "bad_request"));
                
                return Results.BadRequest("Missing Record data.");
            }

            Guid externalId = payload.Record.Id;
            string email = payload.Record.Email ?? string.Empty;
            
            activity?.SetTag("user.id", externalId.ToString());
            activity?.SetTag("user.email", email);

            string provider = payload.Record.AppMetadata?.Provider ?? "email";
            activity?.SetTag("oauth.provider", provider);

            string accountType = payload.Record.UserMetadata?.AccountType ?? string.Empty;
            string firstName = payload.Record.UserMetadata?.FirstName ?? string.Empty;
            string lastName = payload.Record.UserMetadata?.LastName ?? string.Empty;
            string fullName = payload.Record.UserMetadata?.FullName ?? payload.Record.UserMetadata?.Name ?? string.Empty;
            string phoneNumber = payload.Record.UserMetadata?.PhoneNumber ?? string.Empty;
            string avatarUrl = payload.Record.UserMetadata?.AvatarUrl ?? string.Empty;
            bool userAgreementAccepted = payload.Record.UserMetadata?.UserAgreementAccepted ?? false;
            bool kvkkAccepted = payload.Record.UserMetadata?.KvkkAccepted ?? false;
            bool commercialConsentAccepted = payload.Record.UserMetadata?.CommercialConsentAccepted ?? false;

            activity?.SetTag("oauth.account_type", string.IsNullOrWhiteSpace(accountType) ? "none" : accountType);

            if (string.IsNullOrWhiteSpace(accountType))
            {
                logger.LogError("FATAL: Database webhook payload is missing 'account_type' for User ID: {Id}. Provider: '{Provider}'.", externalId, provider);
                
                activity?.SetStatus(ActivityStatusCode.Error, "Missing account_type");
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "missing_account_type"));
                
                return Results.BadRequest("Missing account_type. Cannot determine identity routing.");
            }

            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) && !string.IsNullOrWhiteSpace(fullName))
            {
                string[] nameParts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (nameParts.Length > 1)
                {
                    lastName = nameParts.Last();
                    firstName = string.Join(" ", nameParts.Take(nameParts.Length - 1));
                }
                else
                {
                    firstName = fullName;
                }
            }

            bool isCompany = string.Equals(accountType, "company", StringComparison.OrdinalIgnoreCase) || 
                             string.Equals(accountType, "employer", StringComparison.OrdinalIgnoreCase) || 
                             string.Equals(accountType, "b", StringComparison.OrdinalIgnoreCase);

            bool isAdmin = string.Equals(accountType, "admin", StringComparison.OrdinalIgnoreCase) || 
                           string.Equals(accountType, "super_admin", StringComparison.OrdinalIgnoreCase) || 
                           string.Equals(accountType, "moderator", StringComparison.OrdinalIgnoreCase) || 
                           string.Equals(accountType, "a", StringComparison.OrdinalIgnoreCase);

            activity?.SetTag("routing.is_company", isCompany);
            activity?.SetTag("routing.is_admin", isAdmin);

            bool isNewRecord = false;

            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("[DIAG] publishEndpoint runtime type: {PublishEndpointType}", publishEndpoint.GetType().FullName);
                logger.LogTrace("[DIAG] dbContext instance hash: {DbContextHash}", dbContext.GetHashCode());
                logger.LogTrace("[DIAG] dbContext tracked entries before strategy: {TrackedCount}", dbContext.ChangeTracker.Entries().Count());
            }

            try
            {
                IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace("[DIAG] Execution strategy type: {StrategyType}", strategy.GetType().FullName);

                await strategy.ExecuteAsync(async () =>
                {
                    isNewRecord = false;
                    dbContext.ChangeTracker.Clear();

                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace("[DIAG] ExecuteAsync lambda entered. ChangeTracker cleared. Tracked entries: {TrackedCount}", dbContext.ChangeTracker.Entries().Count());

                    using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace("[DIAG] Transaction begun. TransactionId: {TransactionId}", transaction.TransactionId);

                    try
                    {
                        if (isCompany)
                        {
                            LegacyCompany? existingCompany = await dbContext.Companies
                                .FirstOrDefaultAsync(c => c.ExternalId == externalId || c.Email == email, cancellationToken);

                            if (existingCompany != null)
                            {
                                if (existingCompany.ExternalId != externalId)
                                {
                                    existingCompany.LinkExternalAccount(externalId);
                                    dbContext.Companies.Update(existingCompany);
                                    logger.LogInformation("Migrated Legacy Company: {Email} to External ID: {UserId}", email, externalId);
                                    activity?.AddEvent(new ActivityEvent("Migrated Legacy Company"));
                                }
                                else
                                {
                                    logger.LogTrace("[DIAG] Existing company already has correct ExternalId. No update needed.");
                                }
                            }
                            else
                            {
                                LegacyCompany newCompany = LegacyCompany.CreateFromExternalProvider(
                                    externalId, email, phoneNumber, firstName, lastName, userAgreementAccepted: userAgreementAccepted, kvkkAccepted: kvkkAccepted, commercialConsentAccepted: commercialConsentAccepted
                                );
                                await dbContext.Companies.AddAsync(newCompany, cancellationToken);
                                isNewRecord = true;
                                logger.LogTrace("[DIAG] New LegacyCompany entity added to ChangeTracker.");
                            }
                        }
                        else if (isAdmin)
                        {
                            LegacyAdmin? existingAdmin = await dbContext.Admins
                                .FirstOrDefaultAsync(a => a.ExternalId == externalId || a.Email == email, cancellationToken);

                            if (existingAdmin != null)
                            {
                                if (existingAdmin.ExternalId != externalId)
                                {
                                    existingAdmin.LinkExternalAccount(externalId);
                                    dbContext.Admins.Update(existingAdmin);
                                    logger.LogInformation("Migrated Legacy Admin: {Email} to External ID: {UserId}", email, externalId);
                                    activity?.AddEvent(new ActivityEvent("Migrated Legacy Admin"));
                                }
                                else
                                {
                                    logger.LogTrace("[DIAG] Existing admin already has correct ExternalId. No update needed.");
                                }
                            }
                            else
                            {
                                LegacyAdmin newAdmin = LegacyAdmin.CreateFromExternalProvider(
                                    externalId, email, phoneNumber, firstName, lastName, accountType
                                );
                                await dbContext.Admins.AddAsync(newAdmin, cancellationToken);
                                isNewRecord = true;
                                logger.LogTrace("[DIAG] New LegacyAdmin entity added to ChangeTracker.");
                            }
                        }
                        else
                        {
                            LegacyEmployee? existingEmployee = await dbContext.Employees
                                .FirstOrDefaultAsync(e => e.ExternalId == externalId || e.Email == email, cancellationToken);

                            if (existingEmployee != null)
                            {
                                if (existingEmployee.ExternalId != externalId)
                                {
                                    existingEmployee.LinkExternalAccount(externalId);
                                    dbContext.Employees.Update(existingEmployee);
                                    logger.LogInformation("Migrated Legacy Employee: {Email} to External ID: {UserId}", email, externalId);
                                    activity?.AddEvent(new ActivityEvent("Migrated Legacy Employee"));
                                }
                                else
                                {
                                    logger.LogTrace("[DIAG] Existing employee already has correct ExternalId. No update needed.");
                                }
                            }
                            else
                            {
                                LegacyEmployee newEmployee = LegacyEmployee.CreateFromExternalProvider(
                                    externalId, email, phoneNumber, firstName, lastName, userAgreementAccepted: userAgreementAccepted, kvkkAccepted: kvkkAccepted, commercialConsentAccepted: commercialConsentAccepted
                                );
                                await dbContext.Employees.AddAsync(newEmployee, cancellationToken);
                                isNewRecord = true;
                                logger.LogTrace("[DIAG] New LegacyEmployee entity added to ChangeTracker.");
                            }
                        }

                        if (logger.IsEnabled(LogLevel.Trace))
                        {
                            var entries = dbContext.ChangeTracker.Entries().ToList();
                            logger.LogTrace("[DIAG] ChangeTracker state BEFORE Publish(). Total tracked: {Count}", entries.Count);
                            foreach (var entry in entries)
                                logger.LogTrace("[DIAG]   -> Entity: {EntityType} | State: {State}", entry.Entity.GetType().FullName, entry.State);
                        }

                        if (isNewRecord)
                        {
                            AccountCreatedEvent integrationEvent = new()
                            {
                                UserId = externalId,
                                Email = email,
                                AccountType = accountType,
                                FirstName = firstName,
                                LastName = lastName,
                                PhoneNumber = phoneNumber,
                                AvatarUrl = avatarUrl
                            };

                            logger.LogTrace("[DIAG] Calling publishEndpoint.Publish<AccountCreatedEvent>() for UserId: {UserId}", externalId);
                            await publishEndpoint.Publish(integrationEvent, cancellationToken);
                            logger.LogTrace("[DIAG] publishEndpoint.Publish() returned without exception.");

                            if (logger.IsEnabled(LogLevel.Trace))
                            {
                                var entriesAfterPublish = dbContext.ChangeTracker.Entries().ToList();
                                logger.LogTrace("[DIAG] ChangeTracker state AFTER Publish(). Total tracked: {Count}", entriesAfterPublish.Count);
                                foreach (var entry in entriesAfterPublish)
                                    logger.LogTrace("[DIAG]   -> Entity: {EntityType} | State: {State}", entry.Entity.GetType().FullName, entry.State);

                                // If MassTransit's outbox is working, you should see an OutboxMessage entity with State=Added here.
                                bool hasOutboxMessage = entriesAfterPublish.Any(e => e.Entity.GetType().Name.Contains("OutboxMessage"));
                                logger.LogTrace("[DIAG] OutboxMessage entity present in ChangeTracker after Publish: {HasOutboxMessage}", hasOutboxMessage);
                            }
                        }
                        else
                        {
                            logger.LogTrace("[DIAG] isNewRecord=false — skipping Publish(). This is a migration or no-op path.");
                        }

                        if (logger.IsEnabled(LogLevel.Trace))
                        {
                            var entriesBeforeSave = dbContext.ChangeTracker.Entries().ToList();
                            logger.LogTrace("[DIAG] Calling SaveChangesAsync(). Pending changes: {Count}", entriesBeforeSave.Count);
                            foreach (var entry in entriesBeforeSave)
                                logger.LogTrace("[DIAG]   -> Saving: {EntityType} | State: {State}", entry.Entity.GetType().FullName, entry.State);
                        }

                        int savedCount = await dbContext.SaveChangesAsync(cancellationToken);
                        logger.LogTrace("[DIAG] SaveChangesAsync() completed. Rows affected: {SavedCount}", savedCount);

                        await transaction.CommitAsync(cancellationToken);
                        logger.LogTrace("[DIAG] transaction.CommitAsync() completed. TransactionId: {TransactionId}", transaction.TransactionId);

                        if (isNewRecord)
                        {
                            logger.LogInformation("Transaction committed. Record and outbox event staged for External ID: {Id}", externalId);
                            activity?.AddEvent(new ActivityEvent("Database Commit Successful"));
                        }
                    }
                    catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                    {
                        await transaction.RollbackAsync(cancellationToken);

                        logger.LogError("RACE CONDITION CAUGHT: Thread tried to insert User {Id} but the email is already locked by a concurrent process. Rejecting webhook.", externalId);

                        activity?.SetTag("transaction.outcome", "concurrent_duplicate_rejected");
                        activity?.AddEvent(new ActivityEvent("Idempotency Conflict Triggered"));

                        throw new InvalidOperationException("Concurrent signup collision. Ghost record aborted.");
                    }
                });

                if (isNewRecord)
                {
                    activity?.SetTag("transaction.outcome", "new_record_created");
                }
                else if (activity?.GetTagItem("transaction.outcome") == null)
                {
                    activity?.SetTag("transaction.outcome", "legacy_account_migrated");
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "success"));

                return Results.Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Unhandled database or system failure for External ID: {Id}.", externalId);
                
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "system_failure"));
                
                return Results.StatusCode(StatusCodes.Status500InternalServerError); 
            }
        }).AddEndpointFilter<SupabaseDatabaseWebhookFilter>();
    }
}