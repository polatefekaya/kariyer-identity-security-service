using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.UpdateAdminRole;

public static class UpdateAdminRoleEndpoint
{
    public static RouteHandlerBuilder MapUpdateAdminRole(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/role", async (
            string uid,
            UpdateAdminRoleRequest request,
            IUpdateAdminRoleService handler,
            CancellationToken ct) =>
        {
            // Validated here rather than in the service so an unknown role never
            // reaches the database. The API backend scopes some roles by path and
            // denies by default, so a typo would silently lock the account out of
            // every screen instead of failing loudly at the point of the mistake.
            if (!AdminConstants.IsAllowedRole(request.Role))
            {
                return Results.BadRequest(new ApiResponse<object>(
                    false,
                    $"Geçersiz yönetici rolü. Geçerli roller: {AdminConstants.AllowedRolesDescription}.",
                    null));
            }

            bool success = await handler.HandleAsync(uid, request.Role, ct);

            if (!success) return Results.NotFound(new ApiResponse<object>(false, "Yönetici bulunamadı.", null));

            return Results.Ok(new ApiResponse<object>(true, "Yetki başarıyla güncellendi.", null));
        });
    }
}