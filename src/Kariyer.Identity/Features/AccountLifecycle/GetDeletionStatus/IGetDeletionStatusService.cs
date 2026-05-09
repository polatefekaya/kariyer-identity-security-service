using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

internal interface IGetDeletionStatusService
{
    Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken);
}
