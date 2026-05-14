using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountCredentials.UpdatePassword;

internal sealed class UpdatePasswordService(
    IdentityDbContext dbContext,
    ISupabaseAdminAuthService supabaseAuth,
    ILogger<UpdatePasswordService> logger) : IUpdatePasswordService
{
    public async Task<IResult> HandleAsync(string uid, UpdatePasswordRequest request, ClaimsPrincipal caller, CancellationToken ct)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdatePassword");
        activity?.SetTag("account.uid", uid);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new ApiResponse<object>(false, "Şifre boş olamaz.", null));

            if (request.Password.Length < 8)
                return Results.BadRequest(new ApiResponse<object>(false, "Şifre en az 8 karakter olmalıdır.", null));

            bool hasActiveDeletion = await dbContext.AccountDeletionSagas
                .AnyAsync(s => s.UserUid == uid && AccountDeletionConstants.ActiveDeletionStates.Contains(s.CurrentState), ct);

            if (hasActiveDeletion)
                return Results.Conflict(new ApiResponse<object>(false, "Hesap için aktif bir silme işlemi mevcut. Güncelleme yapılamaz.", null));

            bool existsAsEmployee = await dbContext.Employees.AnyAsync(e => e.Uid == uid, ct);
            string userType = existsAsEmployee ? "employee" : "company";

            Guid externalId;

            if (userType == "employee")
            {
                LegacyEmployee? employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == uid, ct);

                if (employee is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Çalışan bulunamadı.", null));
                if (employee.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Kalıcı silinen hesapta güncelleme yapılamaz.", null));
                if (employee.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Dondurulmuş hesapta güncelleme yapılamaz.", null));
                if (employee.ExternalId is null)
                    return Results.Problem("Hesabın harici kimliği bulunamadı.");

                externalId = employee.ExternalId.Value;
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == uid, ct);

                if (company is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Şirket bulunamadı.", null));
                if (company.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Kalıcı silinen hesapta güncelleme yapılamaz.", null));
                if (company.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Dondurulmuş hesapta güncelleme yapılamaz.", null));
                if (company.ExternalId is null)
                    return Results.Problem("Hesabın harici kimliği bulunamadı.");

                externalId = company.ExternalId.Value;
            }

            await supabaseAuth.UpdatePasswordAsync(externalId, request.Password, ct);

            IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "credential_password_updated"),
                new KeyValuePair<string, object?>("initiated_by", "admin"));

            activity?.AddEvent(new ActivityEvent("PasswordUpdated"));
            logger.LogInformation("Password updated for account {Uid}.", uid);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to update password for account {Uid}.", uid);
            throw;
        }
    }
}
