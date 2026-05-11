using System.Diagnostics;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountCredentials.UpdateEmail;

internal sealed class UpdateEmailService(
    IdentityDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<UpdateEmailService> logger) : IUpdateEmailService
{
    public async Task<IResult> HandleAsync(string uid, UpdateEmailRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateEmail");
        activity?.SetTag("account.uid", uid);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new ApiResponse<object>(false, "E-posta adresi boş olamaz.", null));

            if (request.Email.Length > 254)
                return Results.BadRequest(new ApiResponse<object>(false, "E-posta adresi çok uzun.", null));

            if (!IsValidEmailFormat(request.Email))
                return Results.BadRequest(new ApiResponse<object>(false, "Geçersiz e-posta adresi formatı.", null));

            string? callerSub = caller.FindFirstValue("sub");
            string? callerRole = caller.FindFirstValue("role");
            bool isAdmin = callerRole is "admin" or "super_admin";

            string userType;
            if (!isAdmin)
                userType = callerRole == "company" ? "company" : "employee";
            else
            {
                bool existsAsEmployee = await dbContext.Employees.AnyAsync(e => e.Uid == uid, cancellationToken);
                userType = existsAsEmployee ? "employee" : "company";
            }

            bool hasActiveDeletion = await dbContext.AccountDeletionSagas
                .AnyAsync(s => s.UserUid == uid && AccountDeletionConstants.ActiveDeletionStates.Contains(s.CurrentState), cancellationToken);

            if (hasActiveDeletion)
                return Results.Conflict(new ApiResponse<object>(false, "Hesap için aktif bir silme işlemi mevcut. Güncelleme yapılamaz.", null));

            string newEmail = request.Email.Trim().ToLowerInvariant();

            bool emailTaken = await dbContext.Employees.AnyAsync(e => e.Email == newEmail && e.Uid != uid, cancellationToken)
                           || await dbContext.Companies.AnyAsync(c => c.Email == newEmail && c.Uid != uid, cancellationToken);

            if (emailTaken)
                return Results.Conflict(new ApiResponse<object>(false, "Bu e-posta adresi zaten kullanımda.", null));

            string newEmailHash = ComputeSha256(newEmail);
            string oldEmail;
            string? oldEmailHash;
            Guid externalId;
            string notificationFullName;

            if (userType == "employee")
            {
                LegacyEmployee? employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == uid, cancellationToken);

                if (employee is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Çalışan bulunamadı.", null));
                if (employee.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Kalıcı silinen hesapta güncelleme yapılamaz.", null));
                if (employee.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Dondurulmuş hesapta güncelleme yapılamaz.", null));
                if (!isAdmin && employee.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();
                if (employee.ExternalId is null)
                    return Results.Problem("Hesap harici sağlayıcıya bağlı değil.");
                if (string.Equals(employee.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(new ApiResponse<object>(false, "Yeni e-posta adresi mevcut adresle aynı.", null));

                oldEmail = employee.Email;
                oldEmailHash = employee.EmailHash;
                externalId = employee.ExternalId.Value;
                notificationFullName = $"{employee.Name} {employee.Surname}".Trim();
                employee.UpdateEmail(newEmail, newEmailHash);
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == uid, cancellationToken);

                if (company is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Şirket bulunamadı.", null));
                if (company.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Kalıcı silinen hesapta güncelleme yapılamaz.", null));
                if (company.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Dondurulmuş hesapta güncelleme yapılamaz.", null));
                if (!isAdmin && company.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();
                if (company.ExternalId is null)
                    return Results.Problem("Hesap harici sağlayıcıya bağlı değil.");
                if (string.Equals(company.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(new ApiResponse<object>(false, "Yeni e-posta adresi mevcut adresle aynı.", null));

                oldEmail = company.Email;
                oldEmailHash = company.EmailHash;
                externalId = company.ExternalId.Value;
                notificationFullName = company.CompanyName ?? company.AuthorizedName ?? string.Empty;
                company.UpdateEmail(newEmail, newEmailHash);
            }

            await publishEndpoint.Publish(new InitiateCredentialSupabaseUpdateCommand
            {
                CorrelationId = Guid.NewGuid(),
                UserUid = uid,
                UserType = userType,
                ExternalId = externalId,
                CredentialType = "email",
                NewValue = newEmail,
                NewHash = newEmailHash,
                OldValue = oldEmail,
                OldHash = oldEmailHash,
                InitiatedBy = isAdmin ? callerSub ?? "admin" : "self",
                NotificationEmail = newEmail,
                NotificationFullName = notificationFullName
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "credential_email_updated"),
                new KeyValuePair<string, object?>("initiated_by", isAdmin ? "admin" : "self"));

            activity?.AddEvent(new ActivityEvent("EmailUpdateInitiated"));
            logger.LogInformation("Email update initiated for account {Uid}.", uid);

            return Results.Ok(new ApiResponse<object>(true, "E-posta adresi güncellendi.", null));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to update email for account {Uid}.", uid);
            throw;
        }
    }

    private static bool IsValidEmailFormat(string email)
    {
        try { _ = new MailAddress(email); return true; }
        catch { return false; }
    }

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
