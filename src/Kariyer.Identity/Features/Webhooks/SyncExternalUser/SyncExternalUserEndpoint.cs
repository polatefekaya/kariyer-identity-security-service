using Kariyer.Identity.Domain.Entities;
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

            if (!string.Equals(payload.Metadata?.Name, "before-user-created", StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(payload.Metadata?.Name, "after-user-created", StringComparison.OrdinalIgnoreCase)) 
            {
                logger.LogWarning("Webhook aborted. Expected user creation hook, got '{Name}'.", payload.Metadata?.Name);
                return Results.Json(new { }, contentType: "application/json");
            }

            if (payload.User == null)
            {
                logger.LogError("Webhook payload contained no User data.");
                return Results.BadRequest("Missing User data.");
            }

            Guid externalId = payload.User.Id;
            string email = payload.User.Email ?? string.Empty;
            string accountType = payload.User.UserMetadata?.AccountType ?? "employee";
            string firstName = payload.User.UserMetadata?.FirstName ?? string.Empty;
            string lastName = payload.User.UserMetadata?.LastName ?? string.Empty;
            string phoneNumber = payload.User.UserMetadata?.PhoneNumber ?? string.Empty;

            bool isCompany = string.Equals(accountType, "company", StringComparison.OrdinalIgnoreCase) || 
                             string.Equals(accountType, "employer", StringComparison.OrdinalIgnoreCase) || 
                             string.Equals(accountType, "c", StringComparison.OrdinalIgnoreCase);

            IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
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

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                ExternalUserCreatedEvent integrationEvent = new()
                {
                    UserId = externalId,
                    Email = email,
                    AccountType = accountType,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber
                };

                logger.LogInformation("Integration Event publishing to RabbitMQ for External ID: {Id}", externalId);
                
                _ = publishEndpoint.Publish(integrationEvent, CancellationToken.None);

                return Results.Json(new { }, contentType: "application/json");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogError(ex, "FATAL: Database transaction failed for External ID: {Id}. Rejecting Supabase signup.", externalId);
                
                return Results.StatusCode(500); 
            }
        }).AddEndpointFilter<SupabaseSignatureFilter>();
    }
}