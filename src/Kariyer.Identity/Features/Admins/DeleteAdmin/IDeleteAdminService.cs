namespace Kariyer.Identity.Features.Admins.DeleteAdmin;

internal interface IDeleteAdminService
{
    Task<bool> HandleAsync(string uid, CancellationToken cancellationToken);
}