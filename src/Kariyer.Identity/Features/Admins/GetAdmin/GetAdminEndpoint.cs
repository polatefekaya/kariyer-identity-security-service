using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.GetAdmin;

public static class GetAdminEndpoint
{
    public static RouteHandlerBuilder MapGetAdmin(this IEndpointRouteBuilder app)
    {
        return app.MapGet("/{uid}", async (
            string uid,
            IGetAdminService handler,
            CancellationToken ct) =>
        {
            AdminDto? result = await handler.HandleAsync(uid, ct);

            if (result == null)
            {
                return Results.NotFound(new ApiResponse<object>(false, "Yönetici bulunamadı.", null));
            }

            return Results.Ok(new ApiResponse<AdminDto>(true, "Başarılı", result));
        });
    }
}