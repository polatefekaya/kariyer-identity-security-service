using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.BootstrapAdmin;

internal interface IBootstrapAdminService
{
    Task<ApiResponse<BootstrapAdminResponseData>> HandleAsync(BootstrapAdminRequest request, CancellationToken cancellationToken);
}