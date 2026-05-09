using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.RestoreAccount;

internal interface IRestoreAccountService
{
    Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken);
}
