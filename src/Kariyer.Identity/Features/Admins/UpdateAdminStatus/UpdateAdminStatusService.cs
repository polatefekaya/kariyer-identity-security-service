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
        long startMs = Stopwatch.GetTimestamp();
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateAdminStatus");
        activity?.SetTag("admin.uid", uid);
        activity?.SetTag("admin.new_status", status);

        try
        {
            LegacyAdmin? admin = await dbContext.Admins
                .FirstOrDefaultAsync(a => a.Uid == uid && !a.IsDeleted, cancellationToken);

            if (admin is null || !admin.ExternalId.HasValue)
            {
                activity?.SetTag("admin.found", false);
                return false;
            }

            activity?.SetTag("admin.found", true);
            activity?.SetTag("admin.external_id", admin.ExternalId.Value.ToString());

            bool targetIsActive = status.Equals("active", StringComparison.OrdinalIgnoreCase);

            if (targetIsActive)
                await supabaseAuth.UnbanUserAsync(admin.ExternalId.Value, cancellationToken);
            else
                await supabaseAuth.BanUserAsync(admin.ExternalId.Value, cancellationToken);

            activity?.AddEvent(new ActivityEvent("SupabaseStatusUpdated"));

            admin.UpdateIsActive(targetIsActive);
            await dbContext.SaveChangesAsync(cancellationToken);

            double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;
            IdentityDiagnostics.AdminOperationsCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_status"),
                new KeyValuePair<string, object?>("new_status", targetIsActive ? "active" : "banned"),
                new KeyValuePair<string, object?>("outcome", "success"));
            IdentityDiagnostics.AdminOperationDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("operation", "update_status"));

            activity?.AddEvent(new ActivityEvent("AdminStatusUpdated"));
            return true;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.AdminOperationsCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_status"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }
}
