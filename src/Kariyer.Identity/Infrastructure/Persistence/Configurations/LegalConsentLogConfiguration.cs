using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegalConsentLogConfiguration : IEntityTypeConfiguration<LegalConsentLog>
{
    public void Configure(EntityTypeBuilder<LegalConsentLog> builder)
    {
        builder.ToTable("legal_consent_logs", tableBuilder => tableBuilder.ExcludeFromMigrations());

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(l => l.UserUid).HasColumnName("user_uid").IsRequired();
        builder.Property(l => l.UserType).HasColumnName("user_type").IsRequired();
        builder.Property(l => l.LegalDocId).HasColumnName("legal_doc_id").IsRequired();
        builder.Property(l => l.DocType).HasColumnName("doc_type").IsRequired();
        builder.Property(l => l.DocVersion).HasColumnName("doc_version").IsRequired();
        builder.Property(l => l.Accepted).HasColumnName("accepted").IsRequired();
        builder.Property(l => l.AcceptedAt).HasColumnName("accepted_at").IsRequired();
        builder.Property(l => l.IpAddress).HasColumnName("ip_address").IsRequired(false);
    }
}
