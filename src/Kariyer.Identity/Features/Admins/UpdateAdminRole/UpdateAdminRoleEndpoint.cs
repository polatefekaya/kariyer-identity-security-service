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
            bool success = await handler.HandleAsync(uid, request.Role, ct);

            if (!success) return Results.NotFound(new ApiResponse<object>(false, "Yönetici bulunamadı.", null));

            return Results.Ok(new ApiResponse<object>(true, "Yetki başarıyla güncellendi.", null));
        });
    }
}