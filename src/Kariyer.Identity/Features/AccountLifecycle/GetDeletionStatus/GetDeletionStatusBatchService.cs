using System.Diagnostics;
using System.Security.Claims;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

internal sealed class GetDeletionStatusBatchService(
    IdentityDbContext dbContext,
    ILogger<GetDeletionStatusBatchService> logger) : IGetDeletionStatusBatchService
{
    private const int MaxBatchSize = 100;

    public async Task<IResult> HandleAsync(GetDeletionStatusBatchRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("GetDeletionStatusBatch");

        try
        {
            if (request.Uids is null || request.Uids.Count == 0)
                return Results.Ok(new ApiResponse<Dictionary<string, DeletionStatusDto?>>(true, "Sonuç yok.", []));

            if (request.Uids.Count > MaxBatchSize)
                return Results.BadRequest(new ApiResponse<object>(false, $"En fazla {MaxBatchSize} UID sorgulanabilir.", null));

            List<string> distinctUids = request.Uids.Distinct().ToList();
            activity?.SetTag("batch.uid_count", distinctUids.Count);

            List<AccountDeletionSagaState> sagas = await dbContext.Set<AccountDeletionSagaState>()
                .Where(s => distinctUids.Contains(s.UserUid))
                .ToListAsync(cancellationToken);

            Dictionary<string, DeletionStatusDto> latestPerUid = sagas
                .GroupBy(s => s.UserUid)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        AccountDeletionSagaState s = g.OrderByDescending(x => x.CreatedAt).First();
                        return new DeletionStatusDto(
                            s.UserUid, s.UserType, s.CurrentState, s.InitiatedBy, s.InitiatedByUid,
                            s.GracePeriodEndsAt, s.SupabaseBannedAt, s.ExecutedAt, s.CancelledAt,
                            s.CancelledByUid, s.CreatedAt);
                    });

            Dictionary<string, DeletionStatusDto?> result = distinctUids.ToDictionary(
                uid => uid,
                uid => latestPerUid.TryGetValue(uid, out DeletionStatusDto? dto) ? dto : null);

            return Results.Ok(new ApiResponse<Dictionary<string, DeletionStatusDto?>>(true, "Silme durumları alındı.", result));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to get batch deletion statuses.");
            throw;
        }
    }
}
