using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.CreateAdmin;

public static class CreateAdminEndpoint
{
    public static RouteHandlerBuilder MapCreateAdmin(this IEndpointRouteBuilder app)
    {
        return app.MapPost("/", async (
            CreateAdminRequest request,
            ICreateAdminService handler,
            CancellationToken ct) =>
        {
            ApiResponse<CreateAdminResponseData> result = await handler.HandleAsync(request, ct);
            return Results.Ok(result);
        });
    }
}