using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

internal static class GetDeletionStatusBatchEndpoint
{
    internal static RouteGroupBuilder MapGetDeletionStatusBatch(this RouteGroupBuilder group)
    {
        group.MapPost("deletion-status/batch",
            async (GetDeletionStatusBatchRequest request,
                   IGetDeletionStatusBatchService service,
                   ClaimsPrincipal caller,
                   CancellationToken cancellationToken) =>
                await service.HandleAsync(request, caller, cancellationToken))
            .RequireAuthorization("RequireAdmin");

        return group;
    }
}
