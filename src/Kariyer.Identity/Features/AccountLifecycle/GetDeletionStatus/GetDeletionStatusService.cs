using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

internal sealed class GetDeletionStatusService(
    IdentityDbContext dbContext,
    ILogger<GetDeletionStatusService> logger) : IGetDeletionStatusService
{
    public async Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("GetDeletionStatus");
        activity?.SetTag("account.uid", uid);

        try
        {
            string? callerSub = caller.FindFirstValue("sub");
            string? callerRole = caller.FindFirstValue("role");
            bool isAdmin = callerRole is "admin" or "super_admin";

            if (!isAdmin)
            {
                // Self check: verify caller owns the target account
                string userType = uid.EndsWith("-company") ? "company" : "employee";
                bool ownsAccount = userType == "employee"
                    ? await dbContext.Employees.AnyAsync(e => e.Uid == uid && e.ExternalId.ToString() == callerSub, cancellationToken)
                    : await dbContext.Companies.AnyAsync(c => c.Uid == uid && c.ExternalId.ToString() == callerSub, cancellationToken);

                if (!ownsAccount)
                    return Results.Forbid();
            }

            AccountDeletionSagaState? saga = await dbContext.Set<AccountDeletionSagaState>()
                .Where(s => s.UserUid == uid)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (saga is null)
                return Results.Ok(new ApiResponse<DeletionStatusDto?>(true, "Bu hesap için aktif bir silme işlemi yok.", null));

            DeletionStatusDto dto = new(
                UserUid: saga.UserUid,
                UserType: saga.UserType,
                CurrentState: saga.CurrentState,
                InitiatedBy: saga.InitiatedBy,
                InitiatedByUid: saga.InitiatedByUid,
                GracePeriodEndsAt: saga.GracePeriodEndsAt,
                SupabaseBannedAt: saga.SupabaseBannedAt,
                ExecutedAt: saga.ExecutedAt,
                CancelledAt: saga.CancelledAt,
                CancelledByUid: saga.CancelledByUid,
                CreatedAt: saga.CreatedAt
            );

            return Results.Ok(new ApiResponse<DeletionStatusDto>(true, "Silme durumu alındı.", dto));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to get deletion status for account {Uid}.", uid);
            throw;
        }
    }
}
