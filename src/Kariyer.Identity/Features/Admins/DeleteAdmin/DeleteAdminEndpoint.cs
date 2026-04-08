using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.DeleteAdmin;

public static class DeleteAdminEndpoint
{
    public static RouteHandlerBuilder MapDeleteAdmin(this IEndpointRouteBuilder app)
    {
        return app.MapDelete("/{uid}", async (
            string uid, 
            IDeleteAdminService handler, 
            CancellationToken ct) =>
        {
            bool success = await handler.HandleAsync(uid, ct);
            
            if (!success) return Results.NotFound(new ApiResponse<object>(false, "Yönetici bulunamadı.", null));

            return Results.Ok(new ApiResponse<object>(true, "Yönetici kalıcı olarak silindi.", null));
        });
    }
}