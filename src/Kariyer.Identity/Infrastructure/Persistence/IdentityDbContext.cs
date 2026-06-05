using System.Reflection;
using Kariyer.Identity.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public DbSet<LegacyEmployee> Employees => Set<LegacyEmployee>();
    public DbSet<LegacyCompany> Companies => Set<LegacyCompany>();
    public DbSet<LegacyAdmin> Admins => Set<LegacyAdmin>();
    public DbSet<AccountDeletionSagaState> AccountDeletionSagas => Set<AccountDeletionSagaState>();
    public DbSet<CredentialUpdateSagaState> CredentialUpdateSagas => Set<CredentialUpdateSagaState>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalConsentLog> LegalConsentLogs => Set<LegalConsentLog>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity(b => b.ToTable("inbox_state", "identity"));
        modelBuilder.AddOutboxMessageEntity(b => b.ToTable("outbox_message", "identity"));
        modelBuilder.AddOutboxStateEntity(b => b.ToTable("outbox_state", "identity"));
    }
}