namespace Kariyer.Identity.Features.Admins.UpdateAdminStatus;

internal interface IUpdateAdminStatusService
{
    Task<bool> HandleAsync(string uid, string status, CancellationToken cancellationToken);
}