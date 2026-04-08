using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.DeleteAdmin;

internal sealed class DeleteAdminService(
    IdentityDbContext dbContext, 
    ISupabaseAdminAuthService supabaseAuth,
    ILogger<DeleteAdminService> logger) : IDeleteAdminService
{
    public async Task<bool> HandleAsync(string uid, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("HardDeleteAdmin");
        activity?.SetTag("admin.uid", uid);

        LegacyAdmin? admin = await dbContext.Admins
            .FirstOrDefaultAsync(a => a.Uid == uid, cancellationToken);

        if (admin == null) return false;

        if (admin.ExternalId.HasValue)
        {
            logger.LogWarning("Permanently deleting Supabase Auth user {ExternalId}", admin.ExternalId.Value);
            await supabaseAuth.DeleteUserAsync(admin.ExternalId.Value, cancellationToken);
        }

        dbContext.Admins.Remove(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        IdentityDiagnostics.AdminOperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "hard_delete"));

        return true;
    }
}