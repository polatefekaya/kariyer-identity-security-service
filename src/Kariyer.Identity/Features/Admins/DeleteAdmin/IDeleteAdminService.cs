namespace Kariyer.Identity.Features.Admins.DeleteAdmin;

internal interface IDeleteAdminService
{
    Task<(bool Success, string? Error)> HandleAsync(string uid, Guid? callerExternalId, CancellationToken cancellationToken);
}