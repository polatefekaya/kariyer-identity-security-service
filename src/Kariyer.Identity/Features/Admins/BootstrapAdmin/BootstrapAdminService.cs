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

            IdentityDiagnostics.AdminOperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "bootstrap"));

            return new ApiResponse<BootstrapAdminResponseData>(
                Success: true,
                Message: "Sistem başarıyla başlatıldı ve ilk süper yönetici oluşturuldu.",
                Data: new BootstrapAdminResponseData(newAdmin.Uid)
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to bootstrap system with admin {Email}", request.Email);
            throw; 
        }
    }
}