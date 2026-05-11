using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdateUsername;

internal static class UpdateUsernameEndpoint
{
    public static RouteHandlerBuilder MapUpdateUsername(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/credentials/username", async (
            string uid,
            UpdateUsernameRequest request,
            ClaimsPrincipal caller,
            IUpdateUsernameService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, request, caller, ct);
        });
    }
}
