namespace Kariyer.Identity.Features.Admins.GetCurrentAdmin;

internal interface IGetCurrentAdminService
{
    Task<AdminDto?> HandleAsync(Guid externalId, CancellationToken cancellationToken);
}
