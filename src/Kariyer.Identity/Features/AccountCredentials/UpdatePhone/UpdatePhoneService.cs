using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountCredentials.UpdatePhone;

internal sealed class UpdatePhoneService(
    IdentityDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<UpdatePhoneService> logger) : IUpdatePhoneService
{
    public async Task<IResult> HandleAsync(string uid, UpdatePhoneRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        long startMs = Stopwatch.GetTimestamp();
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdatePhone");
        activity?.SetTag("account.uid", uid);
        activity?.SetTag("credential.field", "phone");

        try
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
                return Results.BadRequest(new ApiResponse<object>(false, "Telefon numarası boş olamaz.", null));

            string newPhone = request.Phone.Trim();

            if (newPhone.Length < 7 || newPhone.Length > 20)
                return Results.BadRequest(new ApiResponse<object>(false, "Telefon numarası 7-20 karakter arasında olmalıdır.", null));

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

            string newPhoneHash = ComputeSha256(newPhone);
            string? oldPhone;
            string? oldPhoneHash;
            Guid externalId;
            string notificationEmail;
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
                if (string.Equals(employee.Phone, newPhone, StringComparison.Ordinal))
                    return Results.Conflict(new ApiResponse<object>(false, "Yeni telefon numarası mevcut numarayla aynı.", null));

                oldPhone = employee.Phone;
                oldPhoneHash = employee.PhoneHash;
                externalId = employee.ExternalId.Value;
                notificationEmail = employee.Email;
                notificationFullName = $"{employee.Name} {employee.Surname}".Trim();
                employee.UpdatePhone(newPhone, newPhoneHash);
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
                if (string.Equals(company.Phone, newPhone, StringComparison.Ordinal))
                    return Results.Conflict(new ApiResponse<object>(false, "Yeni telefon numarası mevcut numarayla aynı.", null));

                oldPhone = company.Phone;
                oldPhoneHash = company.PhoneHash;
                externalId = company.ExternalId.Value;
                notificationEmail = company.Email;
                notificationFullName = company.CompanyName ?? company.AuthorizedName ?? string.Empty;
                company.UpdatePhone(newPhone, newPhoneHash);
            }

            await publishEndpoint.Publish(new InitiateCredentialSupabaseUpdateCommand
            {
                CorrelationId = Guid.NewGuid(),
                UserUid = uid,
                UserType = userType,
                ExternalId = externalId,
                CredentialType = "phone",
                NewValue = newPhone,
                NewHash = newPhoneHash,
                OldValue = oldPhone ?? string.Empty,
                OldHash = oldPhoneHash,
                InitiatedBy = isAdmin ? callerSub ?? "admin" : "self",
                NotificationEmail = notificationEmail,
                NotificationFullName = notificationFullName
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;
            IdentityDiagnostics.CredentialUpdateCounter.Add(1,
                new KeyValuePair<string, object?>("field", "phone"),
                new KeyValuePair<string, object?>("account_type", userType),
                new KeyValuePair<string, object?>("initiated_by", isAdmin ? "admin" : "self"),
                new KeyValuePair<string, object?>("outcome", "success"));
            IdentityDiagnostics.CredentialOperationDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("field", "phone"));

            activity?.AddEvent(new ActivityEvent("PhoneUpdateInitiated"));
            logger.LogInformation("Phone update initiated for account {Uid}.", uid);

            return Results.Ok(new ApiResponse<object>(true, "Telefon numarası güncellendi.", null));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.CredentialUpdateCounter.Add(1,
                new KeyValuePair<string, object?>("field", "phone"),
                new KeyValuePair<string, object?>("outcome", "error"));
            logger.LogError(ex, "Failed to update phone for account {Uid}.", uid);
            throw;
        }
    }

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
