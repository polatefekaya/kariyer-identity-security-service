using Kariyer.Identity.Features.Shared;

namespace Kariyer.Identity.Features.Admins.CreateAdmin;

internal interface ICreateAdminService
{
    Task<ApiResponse<CreateAdminResponseData>> HandleAsync(CreateAdminRequest request, CancellationToken cancellationToken);
}