using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.GetAdmins;

public static class GetAdminsEndpoint
{
    public static RouteHandlerBuilder MapGetAdmins(this IEndpointRouteBuilder app)
    {
        return app.MapGet("/", async (
            string status,
            int page,
            int limit,
            string? search,
            IGetAdminsService handler,
            CancellationToken ct) =>
        {
            int validPage = page < 1 ? 1 : page;
            int validLimit = limit < 1 || limit > 100 ? 10 : limit;
            string validStatus = string.IsNullOrWhiteSpace(status) ? "active" : status;

            PaginatedApiResponse<List<AdminDto>> result = await handler.HandleAsync(validStatus, validPage, validLimit, search, ct);
            return Results.Ok(result);
        });
    }
}