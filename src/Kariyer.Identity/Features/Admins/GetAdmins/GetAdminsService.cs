using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Admins.GetAdmins;

internal sealed class GetAdminsService(IdentityDbContext dbContext) : IGetAdminsService
{
    public async Task<PaginatedApiResponse<List<AdminDto>>> HandleAsync(string status, int page, int limit, string? search, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("GetAdminsList");
        activity?.SetTag("query.status", status);
        activity?.SetTag("query.page", page);

        bool isActiveQuery = status.Equals("active", StringComparison.OrdinalIgnoreCase);

        IQueryable<LegacyAdmin> query = dbContext.Admins
            .AsNoTracking()
            .Where(a => a.IsActive == isActiveQuery && !a.IsDeleted && !a.PermaDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.ToLower();
            query = query.Where(a =>
                (a.Name != null && a.Name.ToLower().Contains(searchLower)) ||
                (a.Surname != null && a.Surname.ToLower().Contains(searchLower)) ||
                (a.Email != null && a.Email.ToLower().Contains(searchLower)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int totalPages = (int)Math.Ceiling(totalCount / (double)limit);

        List<LegacyAdmin> admins = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        List<AdminDto> dtos = admins.Select(a => new AdminDto(
            Uid: a.Uid,
            Name: a.Name ?? string.Empty,
            Surname: a.Surname ?? string.Empty,
            FullName: $"{a.Name} {a.Surname}".Trim(),
            Email: a.Email ?? string.Empty,
            Role: a.AdminRole ?? "admin",
            Status: a.IsActive ? "active" : "passive",
            FormattedLastLogin: a.LastLogin.HasValue ? a.LastLogin.Value.ToString("dd.MM.yyyy HH:mm") : "Hiç giriş yapmadı"
        )).ToList();

        return new PaginatedApiResponse<List<AdminDto>>(
            Success: true,
            Data: dtos,
            Pagination: new PaginationMeta(totalCount, totalPages, page)
        );
    }
}