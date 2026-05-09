using System.Security.Claims;
using Kariyer.Identity.Features.AccountLifecycle.CancelDeletion;

namespace Kariyer.Identity.Features.AccountLifecycle.CancelDeletion;

public static class CancelDeletionEndpoint
{
    public static RouteHandlerBuilder MapCancelDeletion(this IEndpointRouteBuilder app)
    {
        // Admin only — user is banned in Supabase and cannot call this themselves
        return app.MapDelete("/{uid}/deletion", async (
            string uid,
            ClaimsPrincipal caller,
            ICancelDeletionService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, caller, ct);
        })
        .RequireAuthorization("RequireAdmin");
    }
}
