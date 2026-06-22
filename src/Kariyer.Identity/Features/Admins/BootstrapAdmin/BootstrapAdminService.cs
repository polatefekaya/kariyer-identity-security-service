using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.BootstrapAdmin;

internal sealed class BootstrapAdminService(
    IdentityDbContext dbContext,
    ISupabaseAdminAuthService supabaseAuth,
    ILogger<BootstrapAdminService> logger) : IBootstrapAdminService
{
    public async Task<ApiResponse<BootstrapAdminResponseData>> HandleAsync(BootstrapAdminRequest request, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("BootstrapAdmin");

        bool adminExists = await dbContext.Admins
            .AnyAsync(a =>
                a.ExternalId != null &&
                !a.IsDeleted &&
                !a.PermaDeleted,
                cancellationToken);
            
        if (adminExists)
        {
            logger.LogWarning("Bootstrap attempted, but an admin already exists. Operation denied.");
            return new ApiResponse<BootstrapAdminResponseData>(false, "Sistem zaten kurulmuş. İşlem reddedildi.", null);
        }

        if (!AdminConstants.IsAllowedEmailDomain(request.Email))
        {
            logger.LogWarning("Bootstrap rejected: email domain not allowed for {Email}", request.Email);
            return new ApiResponse<BootstrapAdminResponseData>(
                Success: false,
                Message: "Yönetici hesabı yalnızca kurumsal e-posta adresleri ile oluşturulabilir.",
                Data: null);
        }

        try
        {
            logger.LogInformation("Bootstrapping first admin in Supabase Auth for {Email}", request.Email);

            Guid externalId = await supabaseAuth.CreateUserAsync(
                email: request.Email,
                password: request.Password,
                firstName: request.Name,
                lastName: request.Surname,
                accountType: "admin",
                cancellationToken: cancellationToken);

            activity?.AddEvent(new ActivityEvent("BootstrapSupabaseUserCreated"));

            try
            {
                LegacyAdmin newAdmin = LegacyAdmin.CreateFromExternalProvider(
                    externalId: externalId,
                    email: request.Email,
                    phone: string.Empty,
                    name: request.Name,
                    surname: request.Surname,
                    role: "super_admin"
                );

                dbContext.Admins.Add(newAdmin);
                await dbContext.SaveChangesAsync(cancellationToken);

                int adminCount = await dbContext.Admins
                    .CountAsync(a => a.ExternalId != null && !a.IsDeleted && !a.PermaDeleted, cancellationToken);

                if (adminCount > 1)
                {
                    dbContext.Admins.Remove(newAdmin);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await supabaseAuth.DeleteUserAsync(externalId, cancellationToken);
                    logger.LogWarning("Bootstrap race condition detected. Rolled back duplicate admin for {Email}.", request.Email);
                    return new ApiResponse<BootstrapAdminResponseData>(false, "Sistem zaten başka bir istek tarafından kuruldu.", null);
                }

                IdentityDiagnostics.AdminOperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "bootstrap"));

                return new ApiResponse<BootstrapAdminResponseData>(
                    Success: true,
                    Message: "Sistem başarıyla başlatıldı ve ilk süper yönetici oluşturuldu.",
                    Data: new BootstrapAdminResponseData(newAdmin.Uid)
                );
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "DB save failed after Supabase user created for bootstrap {Email}. Compensating by deleting Supabase user.", request.Email);
                try
                {
                    await supabaseAuth.DeleteUserAsync(externalId, cancellationToken);
                    logger.LogInformation("Compensation successful: deleted orphaned Supabase user {ExternalId}", externalId);
                }
                catch (Exception compensationEx)
                {
                    logger.LogCritical(compensationEx, "COMPENSATION FAILED: Orphaned Supabase user {ExternalId} for {Email} could not be deleted.", externalId, request.Email);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to bootstrap system with admin {Email}", request.Email);
            throw;
        }
    }
}