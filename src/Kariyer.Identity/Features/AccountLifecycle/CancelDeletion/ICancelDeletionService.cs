using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.CancelDeletion;

internal interface ICancelDeletionService
{
    Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken);
}
