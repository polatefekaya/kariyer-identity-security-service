using System.Security.Claims;
using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.DeleteAdmin;

public static class DeleteAdminEndpoint
{
    public static RouteHandlerBuilder MapDeleteAdmin(this IEndpointRouteBuilder app)
    {
        return app.MapDelete("/{uid}", async (
            string uid,
            HttpContext httpContext,
            IDeleteAdminService handler,
            CancellationToken ct) =>
        {
            string? callerSub = httpContext.User.FindFirstValue("sub")
                                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            Guid? callerExternalId = Guid.TryParse(callerSub, out Guid parsed) ? parsed : null;

            var (success, error) = await handler.HandleAsync(uid, callerExternalId, ct);

            if (error is not null)
                return Results.Json(
                    new ApiResponse<object>(false, error, null),
                    statusCode: error.Contains("silemezsiniz") ? StatusCodes.Status403Forbidden : StatusCodes.Status404NotFound);

            return Results.Ok(new ApiResponse<object>(true, "Yönetici kalıcı olarak silindi.", null));
        });
    }
}