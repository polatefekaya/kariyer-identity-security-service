using System.Security.Claims;
using Kariyer.Identity.Features.AccountLifecycle.FreezeAccount;

namespace Kariyer.Identity.Features.AccountLifecycle.FreezeAccount;

public static class FreezeAccountEndpoint
{
    public static RouteHandlerBuilder MapFreezeAccount(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/freeze", async (
            string uid,
            ClaimsPrincipal caller,
            IFreezeAccountService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, caller, ct);
        });
    }
}
