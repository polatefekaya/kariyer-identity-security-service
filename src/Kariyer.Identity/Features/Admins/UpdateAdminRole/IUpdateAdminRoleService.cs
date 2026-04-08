namespace Kariyer.Identity.Features.Admins.UpdateAdminRole;

internal interface IUpdateAdminRoleService
{
    Task<bool> HandleAsync(string uid, string newRole, CancellationToken cancellationToken);
}