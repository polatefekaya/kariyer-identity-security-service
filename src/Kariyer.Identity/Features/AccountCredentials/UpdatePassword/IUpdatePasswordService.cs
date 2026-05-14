using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdatePassword;

internal interface IUpdatePasswordService
{
    Task<IResult> HandleAsync(string uid, UpdatePasswordRequest request, ClaimsPrincipal caller, CancellationToken ct);
}

internal record UpdatePasswordRequest(string Password);
