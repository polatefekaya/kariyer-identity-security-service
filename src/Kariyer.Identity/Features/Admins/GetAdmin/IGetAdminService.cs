namespace Kariyer.Identity.Features.Admins.GetAdmin;

internal interface IGetAdminService
{
    Task<AdminDto?> HandleAsync(string uid, CancellationToken cancellationToken);
}