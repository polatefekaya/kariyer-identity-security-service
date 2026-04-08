using System.Security.Cryptography;
using System.Text;
using Kariyer.Identity.Features.Shared;
using Microsoft.Extensions.Primitives;

namespace Kariyer.Identity.Features.Admins.BootstrapAdmin;

public static class BootstrapAdminEndpoint
{
    public static void MapBootstrapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admins/bootstrap", async (
            BootstrapAdminRequest request,
            HttpRequest httpRequest,
            IConfiguration config,
            IBootstrapAdminService handler,
            CancellationToken ct) =>
        {
            string? expectedSecret = config["ExternalProvider:BootstrapSecret"];
            bool hasHeader = httpRequest.Headers.TryGetValue("X-Bootstrap-Secret", out StringValues providedSecret);

            if (!hasHeader || string.IsNullOrWhiteSpace(expectedSecret) || string.IsNullOrWhiteSpace(providedSecret.ToString()))
            {
                return Results.Json(
                    data: new ApiResponse<object>(false, "Bootstrap anahtarı eksik veya yapılandırılmamış.", null), 
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
            byte[] providedBytes = Encoding.UTF8.GetBytes(providedSecret.ToString());

            if (expectedBytes.Length != providedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            {
                return Results.Json(
                    data: new ApiResponse<object>(false, "Geçersiz bootstrap anahtarı.", null), 
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            ApiResponse<BootstrapAdminResponseData> result = await handler.HandleAsync(request, ct);
            
            if (!result.Success)
            {
                return Results.Json(data: result, statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(result);
        });
    }
}