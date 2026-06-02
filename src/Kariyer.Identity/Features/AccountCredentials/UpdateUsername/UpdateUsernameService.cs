using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountCredentials.UpdateUsername;

internal sealed partial class UpdateUsernameService(
    IdentityDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<UpdateUsernameService> logger) : IUpdateUsernameService
{
    [GeneratedRegex(@"^[a-zA-Z0-9_.\-]{3,30}$")]
    private static partial Regex UsernameRegex();

    public async Task<IResult> HandleAsync(string uid, UpdateUsernameRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        long startMs = Stopwatch.GetTimestamp();
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateUsername");
        activity?.SetTag("account.uid", uid);
        activity?.SetTag("credential.field", "username");

        try
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                return Results.BadRequest(new ApiResponse<object>(false, "Kullanıcı adı boş olamaz.", null));

            if (!UsernameRegex().IsMatch(request.Username))
                return Results.BadRequest(new ApiResponse<object>(false, "Kullanıcı adı 3-30 karakter olmalı; harf, rakam, nokta, alt çizgi veya kısa çizgi içerebilir.", null));

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

            bool usernameTaken = await dbContext.Employees
                                    .AnyAsync(e => e.Username == request.Username && e.Uid != uid, cancellationToken)
                              || await dbContext.Companies
                                    .AnyAsync(c => c.Username == request.Username && c.Uid != uid, cancellationToken);

            if (usernameTaken)
                return Results.Conflict(new ApiResponse<object>(false, "Bu kullanıcı adı zaten kullanımda.", null));

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
                if (string.Equals(employee.Username, request.Username, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(new ApiResponse<object>(false, "Yeni kullanıcı adı mevcut adla aynı.", null));

                notificationEmail = employee.Email;
                notificationFullName = $"{employee.Name} {employee.Surname}".Trim();
                employee.UpdateUsername(request.Username);
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
                if (string.Equals(company.Username, request.Username, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(new ApiResponse<object>(false, "Yeni kullanıcı adı mevcut adla aynı.", null));

                notificationEmail = company.Email;
                notificationFullName = company.CompanyName ?? company.AuthorizedName ?? string.Empty;
                company.UpdateUsername(request.Username);
            }

            await publishEndpoint.Publish(new AccountUsernameChangedEvent
            {
                MessageId = Guid.NewGuid().ToString(),
                Uid = uid,
                Email = notificationEmail,
                FullName = notificationFullName,
                NewUsername = request.Username
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;
            IdentityDiagnostics.CredentialUpdateCounter.Add(1,
                new KeyValuePair<string, object?>("field", "username"),
                new KeyValuePair<string, object?>("account_type", userType),
                new KeyValuePair<string, object?>("initiated_by", isAdmin ? "admin" : "self"),
                new KeyValuePair<string, object?>("outcome", "success"));
            IdentityDiagnostics.CredentialOperationDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("field", "username"));

            activity?.AddEvent(new ActivityEvent("UsernameUpdated"));
            logger.LogInformation("Username updated for account {Uid}.", uid);

            return Results.Ok(new ApiResponse<object>(true, "Kullanıcı adı başarıyla güncellendi.", null));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.CredentialUpdateCounter.Add(1,
                new KeyValuePair<string, object?>("field", "username"),
                new KeyValuePair<string, object?>("outcome", "error"));
            logger.LogError(ex, "Failed to update username for account {Uid}.", uid);
            throw;
        }
    }
}
