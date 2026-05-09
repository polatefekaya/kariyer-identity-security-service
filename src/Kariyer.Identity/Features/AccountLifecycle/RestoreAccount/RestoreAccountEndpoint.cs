using System.Security.Claims;
using Kariyer.Identity.Features.AccountLifecycle.RestoreAccount;

namespace Kariyer.Identity.Features.AccountLifecycle.RestoreAccount;

public static class RestoreAccountEndpoint
{
    public static RouteHandlerBuilder MapRestoreAccount(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/restore", async (
            string uid,
            ClaimsPrincipal caller,
            IRestoreAccountService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, caller, ct);
        });
    }
}
