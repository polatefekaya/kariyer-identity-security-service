using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.UpdateAdminStatus;

public static class UpdateAdminStatusEndpoint
{
    public static RouteHandlerBuilder MapUpdateAdminStatus(this IEndpointRouteBuilder app)
    {
        return app.MapPut("/{uid}/status", async (
            string uid, 
            UpdateAdminStatusRequest request, 
            IUpdateAdminStatusService handler, 
            CancellationToken ct) =>
        {
            bool success = await handler.HandleAsync(uid, request.Status, ct);
            
            if (!success) return Results.NotFound(new ApiResponse<object>(false, "Yönetici bulunamadı veya dış kimliği yok.", null));

            string msg = request.Status == "active" ? "Hesap aktifleştirildi." : "Hesap donduruldu.";
            return Results.Ok(new ApiResponse<object>(true, msg, null));
        });
    }
}