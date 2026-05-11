using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdatePhone;

internal static class UpdatePhoneEndpoint
{
    public static RouteHandlerBuilder MapUpdatePhone(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/credentials/phone", async (
            string uid,
            UpdatePhoneRequest request,
            ClaimsPrincipal caller,
            IUpdatePhoneService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, request, caller, ct);
        });
    }
}
