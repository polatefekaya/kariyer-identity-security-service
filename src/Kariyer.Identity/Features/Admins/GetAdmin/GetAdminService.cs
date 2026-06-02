using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.GetAdmin;

internal sealed class GetAdminService(IdentityDbContext dbContext) : IGetAdminService
{
    public async Task<AdminDto?> HandleAsync(string uid, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("GetAdmin");
        activity?.SetTag("admin.uid", uid);

        try
        {
            LegacyAdmin? admin = await dbContext.Admins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Uid == uid && !a.IsDeleted && !a.PermaDeleted, cancellationToken);

            if (admin is null)
            {
                activity?.SetTag("admin.found", false);
                return null;
            }

            activity?.SetTag("admin.found", true);
            activity?.SetTag("admin.role", admin.AdminRole);

            return new AdminDto(
                Uid: admin.Uid,
                Name: admin.Name ?? string.Empty,
                Surname: admin.Surname ?? string.Empty,
                FullName: $"{admin.Name} {admin.Surname}".Trim(),
                Email: admin.Email ?? string.Empty,
                Role: admin.AdminRole ?? "admin",
                Status: admin.IsActive ? "active" : "passive",
                FormattedLastLogin: admin.LastLogin.HasValue ? admin.LastLogin.Value.ToString("dd.MM.yyyy HH:mm") : "Hiç giriş yapmadı"
            );
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
