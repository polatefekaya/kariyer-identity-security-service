using System.Reflection;
using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public DbSet<LegacyEmployee> Employees => Set<LegacyEmployee>();
    public DbSet<LegacyCompany> Companies => Set<LegacyCompany>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
                
        base.OnModelCreating(modelBuilder);
    }
}