using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
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
            [FromBody] SupabaseAuthHookPayload payload,
            IdentityDbContext dbContext,
            IPublishEndpoint publishEndpoint,
            ILogger<SupabaseSignatureFilter> logger,
            CancellationToken cancellationToken) =>
        {
            using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("Supabase.Webhook.UserCreated");
            
            string hookName = payload.Metadata?.Name ?? "unknown";
            string rawUserId = payload.User?.Id.ToString() ?? "unknown";
            
            activity?.SetTag("webhook.name", hookName);
            activity?.SetTag("user.id", rawUserId);

            logger.LogInformation("Webhook received. Hook Name: '{Name}', User ID: '{Id}'", hookName, rawUserId);

            if (!string.Equals(hookName, "before-user-created", StringComparison.OrdinalIgnoreCase)) 
            {
                logger.LogWarning("Webhook aborted. Ignoring hook '{Name}' to prevent ghost records.", hookName);
                
                activity?.SetStatus(ActivityStatusCode.Ok, "Ignored hook type");
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "ignored_hook_type"));
                
                return Results.Json(new { }, contentType: "application/json");
            }

            if (payload.User == null)
            {
                logger.LogError("Webhook payload contained no User data.");
                
                activity?.SetStatus(ActivityStatusCode.Error, "Missing User data");
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "bad_request"));
                
                return Results.BadRequest("Missing User data.");
            }

            Guid externalId = payload.User.Id;
            string email = payload.User.Email ?? string.Empty;
            string? provider = payload.User.AppMetadata?.Provider;
            string? accountType = payload.User.UserMetadata?.AccountType;

            activity?.SetTag("user.email", email);
            activity?.SetTag("oauth.provider", provider ?? "none");

            bool isOAuth = string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase) || 
                           string.Equals(provider, "apple", StringComparison.OrdinalIgnoreCase);

            if (isOAuth)
            {
                accountType = "employee";
                logger.LogInformation("OAuth provider '{Provider}' detected. Hardcoding account_type to 'employee' for User ID: {Id}", provider, externalId);
            }

            activity?.SetTag("oauth.account_type", accountType ?? "none");

            if (string.IsNullOrWhiteSpace(accountType))
            {
                logger.LogError("FATAL: Webhook payload is missing 'account_type' in UserMetadata and Provider is '{Provider}'. Aborting sync for User ID: {Id}", provider ?? "unknown", externalId);
                
                activity?.SetStatus(ActivityStatusCode.Error, "Missing account_type");
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "missing_account_type"));
                
                return Results.BadRequest("Missing account_type. Cannot determine identity routing.");
            }

            string firstName = payload.User.UserMetadata?.FirstName ?? string.Empty;
            string lastName = payload.User.UserMetadata?.LastName ?? string.Empty;
            string? fullName = payload.User.UserMetadata?.FullName ?? payload.User.UserMetadata?.Name;

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

            string phoneNumber = payload.User.UserMetadata?.PhoneNumber ?? string.Empty;
            string avatarUrl = payload.User.UserMetadata?.AvatarUrl ?? string.Empty;

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

            try
            {
                IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    dbContext.ChangeTracker.Clear();

                    using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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
                            }
                            else
                            {
                                LegacyCompany newCompany = LegacyCompany.CreateFromExternalProvider(
                                    externalId, email, phoneNumber, firstName, lastName
                                );
                                await dbContext.Companies.AddAsync(newCompany, cancellationToken);
                                isNewRecord = true;
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
                            }
                            else
                            {
                                LegacyAdmin newAdmin = LegacyAdmin.CreateFromExternalProvider(
                                    externalId, email, phoneNumber, firstName, lastName, accountType
                                );
                                await dbContext.Admins.AddAsync(newAdmin, cancellationToken);
                                isNewRecord = true;
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
                            }
                            else
                            {
                                LegacyEmployee newEmployee = LegacyEmployee.CreateFromExternalProvider(
                                    externalId, email, phoneNumber, firstName, lastName
                                );
                                await dbContext.Employees.AddAsync(newEmployee, cancellationToken);
                                isNewRecord = true;
                            }
                        }

                        await dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        
                        if (isNewRecord)
                        {
                            logger.LogInformation("Database commit successful. New record created for External ID: {Id}", externalId);
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

                    ExternalUserCreatedEvent integrationEvent = new()
                    {
                        UserId = externalId,
                        Email = email,
                        AccountType = accountType,
                        FirstName = firstName,
                        LastName = lastName,
                        PhoneNumber = phoneNumber,
                        AvatarUrl = avatarUrl
                    };
                    
                    logger.LogInformation("Integration Event publishing to RabbitMQ for External ID: {Id}", externalId);
                    await publishEndpoint.Publish(integrationEvent, cancellationToken);
                }
                else if (activity?.GetTagItem("transaction.outcome") == null)
                {
                    activity?.SetTag("transaction.outcome", "legacy_account_migrated");
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "success"));

                var responsePayload = new
                {
                    user_metadata = new Dictionary<string, string>
                    {
                        { "account_type", accountType },
                        { "first_name", firstName },
                        { "last_name", lastName },
                        { "full_name", string.IsNullOrWhiteSpace(fullName) ? $"{firstName} {lastName}".Trim() : fullName }
                    }
                };

                return Results.Json(responsePayload, contentType: "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Unhandled database or system failure for External ID: {Id}. Rejecting Supabase signup.", externalId);
                
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                IdentityDiagnostics.WebhookProcessedCounter.Add(1, new KeyValuePair<string, object?>("outcome", "system_failure"));
                
                return Results.StatusCode(StatusCodes.Status500InternalServerError); 
            }
        }).AddEndpointFilter<SupabaseSignatureFilter>();
    }
}