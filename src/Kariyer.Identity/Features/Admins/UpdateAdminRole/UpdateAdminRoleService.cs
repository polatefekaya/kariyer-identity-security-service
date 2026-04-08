using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.UpdateAdminRole;

internal sealed class UpdateAdminRoleService(IdentityDbContext dbContext) : IUpdateAdminRoleService
{
    public async Task<bool> HandleAsync(string uid, string newRole, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateAdminRole");
        activity?.SetTag("admin.uid", uid);
        activity?.SetTag("admin.new_role", newRole);

        LegacyAdmin? admin = await dbContext.Admins
            .FirstOrDefaultAsync(a => a.Uid == uid && !a.IsDeleted, cancellationToken);

        if (admin == null) return false;

        admin.UpdateRole(newRole);

        await dbContext.SaveChangesAsync(cancellationToken);
        IdentityDiagnostics.AdminOperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "update_role"));

        return true;
    }
}