using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountLifecycle.RequestDeletion;

internal sealed class RequestDeletionService(
    IdentityDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<RequestDeletionService> logger) : IRequestDeletionService
{
    private static readonly string[] ActiveStates = ["DeletionRequested", "GracePeriodActive", "Executing", "Cancelling"];

    public async Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("RequestAccountDeletion");
        activity?.SetTag("account.uid", uid);

        try
        {
            string? callerSub = caller.FindFirstValue("sub");
            string? callerRole = caller.FindFirstValue("role");
            bool isAdmin = callerRole is "admin" or "super_admin";
            string initiatedBy = isAdmin ? "admin" : "self";
            string? initiatedByUid = isAdmin ? callerSub : null;

            activity?.SetTag("caller.is_admin", isAdmin.ToString());

            string userType;
            if (!isAdmin)
                userType = callerRole == "company" ? "company" : "employee";
            else
            {
                bool existsAsEmployee = await dbContext.Employees.AnyAsync(e => e.Uid == uid, cancellationToken);
                userType = existsAsEmployee ? "employee" : "company";
            }
            activity?.SetTag("account.type", userType);

            Guid externalId;

            if (userType == "employee")
            {
                LegacyEmployee? employee = await dbContext.Employees
                    .FirstOrDefaultAsync(e => e.Uid == uid, cancellationToken);

                if (employee is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Çalışan bulunamadı.", null));

                if (employee.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap zaten kalıcı olarak silinmiş.", null));

                if (!isAdmin && employee.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();

                if (employee.ExternalId is null)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesabın Supabase bağlantısı yok.", null));

                externalId = employee.ExternalId.Value;
            }
            else
            {
                LegacyCompany? company = await dbContext.Companies
                    .FirstOrDefaultAsync(c => c.Uid == uid, cancellationToken);

                if (company is null)
                    return Results.NotFound(new ApiResponse<object>(false, "Şirket bulunamadı.", null));

                if (company.PermaDeleted)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesap zaten kalıcı olarak silinmiş.", null));

                if (!isAdmin && company.ExternalId?.ToString() != callerSub)
                    return Results.Forbid();

                if (company.ExternalId is null)
                    return Results.Conflict(new ApiResponse<object>(false, "Hesabın Supabase bağlantısı yok.", null));

                externalId = company.ExternalId.Value;
            }

            // Enforce: only one active deletion saga per user
            bool hasActiveSaga = await dbContext.Set<AccountDeletionSagaState>()
                .AnyAsync(s => s.UserUid == uid && ActiveStates.Contains(s.CurrentState), cancellationToken);

            if (hasActiveSaga)
                return Results.Conflict(new ApiResponse<object>(false, "Bu hesap için zaten aktif bir silme işlemi mevcut.", null));

            Guid correlationId = Guid.NewGuid();

            await publishEndpoint.Publish(new RequestAccountDeletionCommand
            {
                CorrelationId = correlationId,
                UserUid = uid,
                UserType = userType,
                ExternalId = externalId,
                InitiatedBy = initiatedBy,
                InitiatedByUid = initiatedByUid
            }, cancellationToken);

            // SaveChanges flushes the outbox message to DB atomically
            await dbContext.SaveChangesAsync(cancellationToken);

            activity?.SetTag("saga.correlation_id", correlationId.ToString());
            activity?.AddEvent(new ActivityEvent("DeletionRequestPublished"));
            logger.LogInformation(
                "Permanent deletion requested for account {Uid} by {InitiatedBy}. SagaId: {CorrelationId}.",
                uid, initiatedBy, correlationId);

            string message = isAdmin
                ? "Hesap kalıcı silme işlemi başlatıldı."
                : "Silme isteğiniz alındı. Hesabınız 30 gün içinde silinecek. Bu süre zarfında destek ekibimizle iletişime geçerek iptal edebilirsiniz.";

            return Results.Accepted(value: new ApiResponse<object>(true, message, new { CorrelationId = correlationId }));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to request deletion for account {Uid}.", uid);
            throw;
        }
    }
}
