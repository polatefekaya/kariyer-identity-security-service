using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;

internal interface IGetDeletionStatusBatchService
{
    Task<IResult> HandleAsync(GetDeletionStatusBatchRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken);
}
