using System.Security.Claims;
using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.GetCurrentAdmin;

public static class GetCurrentAdminEndpoint
{
    public static RouteHandlerBuilder MapGetCurrentAdmin(this IEndpointRouteBuilder app)
    {
        return app.MapGet("/me", async (
            HttpContext httpContext,
            IGetCurrentAdminService handler,
            CancellationToken ct) =>
        {
            string? sub = httpContext.User.FindFirstValue("sub")
                          ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out Guid externalId))
            {
                return Results.Unauthorized();
            }

            AdminDto? result = await handler.HandleAsync(externalId, ct);

            if (result is null)
            {
                return Results.Json(
                    new ApiResponse<object>(false, "Aktif yönetici kaydı bulunamadı.", null),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(new ApiResponse<AdminDto>(true, "Başarılı", result));
        });
    }
}
