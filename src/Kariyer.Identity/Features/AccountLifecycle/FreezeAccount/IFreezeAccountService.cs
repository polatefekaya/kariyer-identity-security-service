using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountLifecycle.FreezeAccount;

internal interface IFreezeAccountService
{
    Task<IResult> HandleAsync(string uid, ClaimsPrincipal caller, CancellationToken cancellationToken);
}
