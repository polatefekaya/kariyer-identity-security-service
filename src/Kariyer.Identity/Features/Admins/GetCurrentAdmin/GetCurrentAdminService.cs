using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.GetCurrentAdmin;

internal sealed class GetCurrentAdminService(IdentityDbContext dbContext) : IGetCurrentAdminService
{
    public async Task<AdminDto?> HandleAsync(Guid externalId, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("GetCurrentAdmin");
        activity?.SetTag("admin.external_id", externalId.ToString());

        LegacyAdmin? admin = await dbContext.Admins
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ExternalId == externalId && a.IsActive && !a.IsDeleted, cancellationToken);

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
}
