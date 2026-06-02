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
        long startMs = Stopwatch.GetTimestamp();
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("HardDeleteAdmin");
        activity?.SetTag("admin.uid", uid);

        try
        {
            LegacyAdmin? admin = await dbContext.Admins
                .FirstOrDefaultAsync(a => a.Uid == uid, cancellationToken);

            if (admin is null)
            {
                activity?.SetTag("admin.found", false);
                return false;
            }

            activity?.SetTag("admin.found", true);
            activity?.SetTag("admin.role", admin.AdminRole);
            activity?.SetTag("admin.has_external_id", admin.ExternalId.HasValue.ToString());

            if (admin.ExternalId.HasValue)
            {
                logger.LogWarning("Permanently deleting Supabase Auth user {ExternalId}", admin.ExternalId.Value);
                await supabaseAuth.DeleteUserAsync(admin.ExternalId.Value, cancellationToken);
                activity?.AddEvent(new ActivityEvent("SupabaseUserDeleted"));
            }

            dbContext.Admins.Remove(admin);
            await dbContext.SaveChangesAsync(cancellationToken);

            double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;
            IdentityDiagnostics.AdminOperationsCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "hard_delete"),
                new KeyValuePair<string, object?>("outcome", "success"));
            IdentityDiagnostics.AdminOperationDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("operation", "hard_delete"));

            activity?.AddEvent(new ActivityEvent("AdminDeleted"));
            return true;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.AdminOperationsCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "hard_delete"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }
}
