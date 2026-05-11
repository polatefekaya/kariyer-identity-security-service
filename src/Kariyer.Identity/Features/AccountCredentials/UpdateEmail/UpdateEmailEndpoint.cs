using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdateEmail;

internal static class UpdateEmailEndpoint
{
    public static RouteHandlerBuilder MapUpdateEmail(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/credentials/email", async (
            string uid,
            UpdateEmailRequest request,
            ClaimsPrincipal caller,
            IUpdateEmailService service,
            CancellationToken ct) =>
        {
            return await service.HandleAsync(uid, request, caller, ct);
        });
    }
}
