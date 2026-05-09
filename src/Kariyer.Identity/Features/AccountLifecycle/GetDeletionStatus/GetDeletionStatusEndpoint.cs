using System.Security.Claims;
using Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

namespace Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

public static class GetDeletionStatusEndpoint
{
    public static RouteHandlerBuilder MapGetDeletionStatus(this IEndpointRouteBuilder app)
    {
        return app.MapGet("/{uid}/deletion", async (
            string uid,
            ClaimsPrincipal caller,
            IGetDeletionStatusService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, caller, ct);
        });
    }
}
