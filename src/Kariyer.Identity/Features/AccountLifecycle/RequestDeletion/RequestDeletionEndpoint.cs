using System.Security.Claims;
using Kariyer.Identity.Features.AccountLifecycle.RequestDeletion;

namespace Kariyer.Identity.Features.AccountLifecycle.RequestDeletion;

public static class RequestDeletionEndpoint
{
    public static RouteHandlerBuilder MapRequestDeletion(this IEndpointRouteBuilder app)
    {
        return app.MapPost("/{uid}/deletion", async (
            string uid,
            ClaimsPrincipal caller,
            IRequestDeletionService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, caller, ct);
        });
    }
}
