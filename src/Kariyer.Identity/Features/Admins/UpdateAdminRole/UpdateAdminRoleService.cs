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
        long startMs = Stopwatch.GetTimestamp();
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("UpdateAdminRole");
        activity?.SetTag("admin.uid", uid);
        activity?.SetTag("admin.new_role", newRole);

        try
        {
            LegacyAdmin? admin = await dbContext.Admins
                .FirstOrDefaultAsync(a => a.Uid == uid && !a.IsDeleted, cancellationToken);

            if (admin is null)
            {
                activity?.SetTag("admin.found", false);
                return false;
            }

            activity?.SetTag("admin.found", true);
            activity?.SetTag("admin.previous_role", admin.AdminRole);
            admin.UpdateRole(newRole);

            await dbContext.SaveChangesAsync(cancellationToken);

            double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;
            IdentityDiagnostics.AdminOperationsCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_role"),
                new KeyValuePair<string, object?>("outcome", "success"));
            IdentityDiagnostics.AdminOperationDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("operation", "update_role"));

            activity?.AddEvent(new ActivityEvent("AdminRoleUpdated"));
            return true;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            IdentityDiagnostics.AdminOperationsCounter.Add(1,
                new KeyValuePair<string, object?>("operation", "update_role"),
                new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }
}
