using System.Security.Claims;
using Kariyer.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Infrastructure.Auth;

public class AdminDbAuthorizationHandler(
    IdentityDbContext dbContext,
    ILogger<AdminDbAuthorizationHandler> logger) : AuthorizationHandler<AdminDbRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminDbRequirement requirement)
    {
        string? sub = context.User.FindFirstValue("sub")
                      ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out Guid externalId))
        {
            logger.LogWarning("AdminDbAuth: Missing or invalid 'sub' claim.");
            return;
        }

        bool exists = await dbContext.Admins
            .AsNoTracking()
            .AnyAsync(a => a.ExternalId == externalId && a.IsActive && !a.IsDeleted);

        if (exists)
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning("AdminDbAuth: No active admin record for ExternalId {ExternalId}.", externalId);
        }
    }
}
