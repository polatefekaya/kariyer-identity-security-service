using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdatePhone;

internal interface IUpdatePhoneService
{
    Task<IResult> HandleAsync(string uid, UpdatePhoneRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken);
}

internal record UpdatePhoneRequest(string Phone);
