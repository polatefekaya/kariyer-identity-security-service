using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountLifecycle.FreezeAccount;

internal sealed class FreezeAccountService(
    IdentityDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<FreezeAccountService> logger) : IFreezeAccountService
{
    private static readonly string[] ActiveDeletionStates = ["DeletionRequested", "GracePeriodActive", "Executing", "Cancelling"];

    public async Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("FreezeAccount");
        activity?.SetTag("account.uid", uid);

        try
        {
            string? callerSub = caller.FindFirstValue("sub");
            string? callerRole = caller.FindFirstValue("role");
            bool isAdmin = callerRole is "admin" or "super_admin";
            string initiatedBy = isAdmin ? callerSub ?? "admin" : callerSub ?? "self";

            activity?.SetTag("caller.role", callerRole);
            activity?.SetTag("caller.is_admin", isAdmin.ToString());

            string userType = uid.EndsWith("-company") ? "company" : "employee";
            activity?.SetTag("account.type", userType);

            bool hasActiveDeletion = await dbContext.AccountDeletionSagas
                .AnyAsync(s => s.UserUid == uid && ActiveDeletionStates.Contains(s.CurrentState), cancellationToken);

            if (hasActiveDeletion)
                return Results.Conflict(new ApiResponse<object>(false, "Hesap için aktif bir silme işlemi mevcut. Dondurma işlemi yapılamaz.", null));

            if (userType == "employee")
            {
                LegacyEmployee? employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == uid, cancellationToken);

                if (employee is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Çalışan bulunamadı.", null));

                if (employee.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap kalıcı olarak silinmiş.", null));

                if (employee.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap zaten dondurulmuş.", null));

                if (!isAdmin && employee.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();

                employee.Freeze(initiatedBy);

                await publishEndpoint.Publish(new AccountFrozenEvent
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Uid = uid,
                    Email = employee.Email,
                    FullName = $"{employee.Name} {employee.Surname}".Trim(),
                    Reason = isAdmin ? "admin_initiated" : "self_initiated"
                }, cancellationToken);
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == uid, cancellationToken);

                if (company is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Şirket bulunamadı.", null));

                if (company.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap kalıcı olarak silinmiş.", null));

                if (company.IsDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap zaten dondurulmuş.", null));

                if (!isAdmin && company.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();

                company.Freeze(initiatedBy);

                await publishEndpoint.Publish(new AccountFrozenEvent
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Uid = uid,
                    Email = company.Email,
                    FullName = company.CompanyName ?? company.AuthorizedName ?? string.Empty,
                    Reason = isAdmin ? "admin_initiated" : "self_initiated"
                }, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "freeze"),
                new KeyValuePair<string, object?>("user_type", userType),
                new KeyValuePair<string, object?>("initiated_by", isAdmin ? "admin" : "self"));

            activity?.AddEvent(new ActivityEvent("AccountFrozen"));
            logger.LogInformation("Account {Uid} frozen by {InitiatedBy}.", uid, initiatedBy);

            return Results.Ok(new ApiResponse<object>(true, "Hesap başarıyla donduruldu.", null));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to freeze account {Uid}.", uid);
            throw;
        }
    }
}
