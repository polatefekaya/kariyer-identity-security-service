using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountLifecycle.CancelDeletion;

internal sealed class CancelDeletionService(
    IdentityDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<CancelDeletionService> logger) : ICancelDeletionService
{
    public async Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("CancelAccountDeletion");
        activity?.SetTag("account.uid", uid);

        try
        {
            string? callerSub = caller.FindFirstValue("sub");
            activity?.SetTag("caller.uid", callerSub);

            AccountDeletionSagaState? saga = await dbContext.Set<AccountDeletionSagaState>()
                .FirstOrDefaultAsync(s => s.UserUid == uid && s.CurrentState == "GracePeriodActive", cancellationToken);

            if (saga is null)
                return Results.NotFound(new ApiResponse<object>(false,
                    "Bu hesap için iptal edilebilir aktif bir silme işlemi bulunamadı.", null));

            await publishEndpoint.Publish(new CancelAccountDeletionCommand
            {
                UserUid = uid,
                CancelledByUid = callerSub ?? string.Empty
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            activity?.SetTag("saga.correlation_id", saga.CorrelationId.ToString());
            activity?.AddEvent(new ActivityEvent("DeletionCancellationPublished"));
            logger.LogInformation(
                "Deletion cancellation published for account {Uid} by admin {AdminUid}. SagaId: {CorrelationId}.",
                uid, callerSub, saga.CorrelationId);

            return Results.Ok(new ApiResponse<object>(true, "Silme iptali işleme alındı. Hesap kısa süre içinde aktif olacak.", null));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to cancel deletion for account {Uid}.", uid);
            throw;
        }
    }
}
