using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.GetAdmin;

internal sealed class GetAdminService(IdentityDbContext dbContext) : IGetAdminService
{
    public async Task<AdminDto?> HandleAsync(string uid, CancellationToken cancellationToken)
    {
        LegacyAdmin? admin = await dbContext.Admins
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Uid == uid && !a.IsDeleted && !a.PermaDeleted, cancellationToken);

        if (admin == null) return null;

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