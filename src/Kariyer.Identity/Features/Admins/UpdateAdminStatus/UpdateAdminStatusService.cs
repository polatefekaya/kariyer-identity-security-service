using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.UpdateAdminStatus;

internal sealed class UpdateAdminStatusService(
    IdentityDbContext dbContext, 
    ISupabaseAdminAuthService supabaseAuth) : IUpdateAdminStatusService
{
    public async Task<bool> HandleAsync(string uid, string status, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateAdminStatus");
        activity?.SetTag("admin.uid", uid);
        activity?.SetTag("admin.new_status", status);

        LegacyAdmin? admin = await dbContext.Admins
            .FirstOrDefaultAsync(a => a.Uid == uid && !a.IsDeleted, cancellationToken);

        if (admin == null || !admin.ExternalId.HasValue) return false;

        bool targetIsActive = status.Equals("active", StringComparison.OrdinalIgnoreCase);

        if (targetIsActive)
        {
            await supabaseAuth.UnbanUserAsync(admin.ExternalId.Value, cancellationToken);
        }
        else
        {
            await supabaseAuth.BanUserAsync(admin.ExternalId.Value, cancellationToken);
        }

        admin.UpdateIsActive(targetIsActive);

        await dbContext.SaveChangesAsync(cancellationToken);
        IdentityDiagnostics.AdminOperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "update_status"));

        return true;
    }
}