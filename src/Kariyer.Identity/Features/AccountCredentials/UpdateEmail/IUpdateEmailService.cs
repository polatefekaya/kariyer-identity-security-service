using System.Security.Claims;

namespace Kariyer.Identity.Features.AccountCredentials.UpdateEmail;

internal interface IUpdateEmailService
{
    Task<IResult> HandleAsync(string uid, UpdateEmailRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken);
}

internal record UpdateEmailRequest(string Email);
