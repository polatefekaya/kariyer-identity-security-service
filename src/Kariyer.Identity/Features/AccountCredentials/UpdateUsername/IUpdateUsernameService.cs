using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdateUsername;

internal interface IUpdateUsernameService
{
    Task<IResult> HandleAsync(string uid, UpdateUsernameRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken);
}

internal record UpdateUsernameRequest(string Username);
