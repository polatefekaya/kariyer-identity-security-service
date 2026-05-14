using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdatePassword;

internal static class UpdatePasswordEndpoint
{
    public static RouteHandlerBuilder MapUpdatePassword(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/password", async (
            string uid,
            UpdatePasswordRequest request,
            ClaimsPrincipal caller,
            IUpdatePasswordService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, request, caller, ct);
        })
        .RequireAuthorization("RequireAdmin")
        .RequireRateLimiting("AdminOperations");
    }
}
