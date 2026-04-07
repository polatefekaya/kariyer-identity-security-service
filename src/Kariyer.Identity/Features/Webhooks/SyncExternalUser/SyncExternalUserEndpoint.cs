using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Account.AccountOAuthCreated;
using Kariyer.Identity.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
            logger.LogInformation("Webhook received. Hook Name: '{Name}', User ID: '{Id}'", 
                payload.Metadata?.Name ?? "NULL", 
                payload.User?.Id.ToString() ?? "NULL");

            if (!string.Equals(payload.Metadata?.Name, "before-user-created", StringComparison.OrdinalIgnoreCase)) 
            {
                logger.LogWarning("Webhook aborted. Ignoring hook '{Name}' to prevent ghost records.", payload.Metadata?.Name);
                return Results.Json(new { }, contentType: "application/json");
            }

            if (payload.User == null)
            {
                logger.LogError("Webhook payload contained no User data.");
                return Results.BadRequest("Missing User data.");
            }

            Guid externalId = payload.User.Id;
            string email = payload.User.Email ?? string.Empty;
            
            string? provider = payload.User.AppMetadata?.Provider;
            string? accountType = payload.User.UserMetadata?.AccountType;

            if (string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(provider, "apple", StringComparison.OrdinalIgnoreCase))
            {
                accountType = "employee";
                logger.LogInformation("OAuth provider '{Provider}' detected. Hardcoding account_type to 'employee' for User ID: {Id}", provider, externalId);
            }

            if (string.IsNullOrWhiteSpace(accountType))
            {
                logger.LogError("FATAL: Webhook payload is missing 'account_type' in UserMetadata and Provider is '{Provider}'. Aborting sync for User ID: {Id}", provider ?? "unknown", externalId);
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

            try
            {
                IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    dbContext.ChangeTracker.Clear();

                    using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                    if (isCompany)
                    {
                        LegacyCompany? existingCompany = await dbContext.Companies
                            .FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

                        if (existingCompany != null)
                        {
                            if (existingCompany.ExternalId != externalId)
                            {
                                existingCompany.LinkExternalAccount(externalId);
                                dbContext.Companies.Update(existingCompany);
                                logger.LogInformation("Migrated Legacy Company: {Email} to External ID: {UserId}", email, externalId);
                            }
                        }
                        else
                        {
                            LegacyCompany newCompany = LegacyCompany.CreateFromExternalProvider(
                                externalId, email, phoneNumber, firstName, lastName
                            );
                            await dbContext.Companies.AddAsync(newCompany, cancellationToken);
                            logger.LogInformation("Created New Company: {Email}. Pending frontend onboarding.", email);
                        }
                    }
                    else if (isAdmin)
                    {
                        LegacyAdmin? existingAdmin = await dbContext.Admins
                            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

                        if (existingAdmin != null)
                        {
                            if (existingAdmin.ExternalId != externalId)
                            {
                                existingAdmin.LinkExternalAccount(externalId);
                                dbContext.Admins.Update(existingAdmin);
                                logger.LogInformation("Migrated Legacy Admin: {Email} to External ID: {UserId}", email, externalId);
                            }
                        }
                        else
                        {
                            LegacyAdmin newAdmin = LegacyAdmin.CreateFromExternalProvider(
                                externalId, email, phoneNumber, firstName, lastName, accountType
                            );
                            await dbContext.Admins.AddAsync(newAdmin, cancellationToken);
                            logger.LogInformation("Created New Admin in local DB: {Email}.", email);
                        }
                    }
                    else 
                    {
                        LegacyEmployee? existingEmployee = await dbContext.Employees
                            .FirstOrDefaultAsync(e => e.Email == email, cancellationToken);

                        if (existingEmployee != null)
                        {
                            if (existingEmployee.ExternalId != externalId)
                            {
                                existingEmployee.LinkExternalAccount(externalId);
                                dbContext.Employees.Update(existingEmployee);
                                logger.LogInformation("Migrated Legacy Employee: {Email} to External ID: {UserId}", email, externalId);
                            }
                        }
                        else
                        {
                            LegacyEmployee newEmployee = LegacyEmployee.CreateFromExternalProvider(
                                externalId, email, phoneNumber, firstName, lastName
                            );
                            await dbContext.Employees.AddAsync(newEmployee, cancellationToken);
                            logger.LogInformation("Created New Employee: {Email}. Pending frontend onboarding.", email);
                        }
                    }

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

                    AccountOAuthCreatedEvent oauthEvent = new()
                    {
                        UserId = externalId,
                        Provider = provider ?? "unknown",
                        AccountType = accountType
                    };
                    
                    logger.LogInformation("Integration Event publishing to RabbitMQ for External ID: {Id}", externalId);
                    logger.LogInformation("Publishing AccountOAuthCreatedEvent to RabbitMQ for External ID: {Id}", externalId);

                    await publishEndpoint.Publish(integrationEvent, cancellationToken);
                    await publishEndpoint.Publish(oauthEvent, cancellationToken);

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                });

                return Results.Json(new { }, contentType: "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Database transaction failed for External ID: {Id}. Rejecting Supabase signup.", externalId);
                return Results.StatusCode(500); 
            }
        }).AddEndpointFilter<SupabaseSignatureFilter>();
    }
}