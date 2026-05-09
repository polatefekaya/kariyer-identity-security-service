using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountLifecycle.RestoreAccount;

internal sealed class RestoreAccountService(
    IdentityDbContext dbContext,
    ILogger<RestoreAccountService> logger) : IRestoreAccountService
{
    public async Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("RestoreAccount");
        activity?.SetTag("account.uid", uid);

        try
        {
            string? callerSub = caller.FindFirstValue("sub");
            string? callerRole = caller.FindFirstValue("role");
            bool isAdmin = callerRole is "admin" or "super_admin";

            activity?.SetTag("caller.is_admin", isAdmin.ToString());

            string userType = uid.EndsWith("-company") ? "company" : "employee";
            activity?.SetTag("account.type", userType);

            if (userType == "employee")
            {
                LegacyEmployee? employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == uid, cancellationToken);

                if (employee is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Çalışan bulunamadı.", null));

                if (employee.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Kalıcı silinen hesap geri yüklenemez.", null));

                if (!employee.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap zaten aktif.", null));

                if (!isAdmin && employee.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();

                employee.Restore();
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == uid, cancellationToken);

                if (company is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Şirket bulunamadı.", null));

                if (company.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Kalıcı silinen hesap geri yüklenemez.", null));

                if (!company.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap zaten aktif.", null));

                if (!isAdmin && company.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();

                company.Restore();
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "restore"),
                new KeyValuePair<string, object?>("user_type", userType));

            activity?.AddEvent(new ActivityEvent("AccountRestored"));
            logger.LogInformation("Account {Uid} restored.", uid);

            return Results.Ok(new ApiResponse<object>(true, "Hesap başarıyla aktif edildi.", null));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to restore account {Uid}.", uid);
            throw;
        }
    }
}
