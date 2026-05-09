using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.RequestDeletion;

internal interface IRequestDeletionService
{
    Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken);
}
