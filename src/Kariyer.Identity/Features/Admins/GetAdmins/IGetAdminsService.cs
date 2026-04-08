using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.GetAdmins;

internal interface IGetAdminsService
{
    Task<PaginatedApiResponse<List<AdminDto>>> HandleAsync(string status, int page, int limit, string? search, CancellationToken cancellationToken);
}