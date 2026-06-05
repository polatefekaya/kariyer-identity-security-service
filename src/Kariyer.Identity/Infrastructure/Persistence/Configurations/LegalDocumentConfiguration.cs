using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("legal_documents", tableBuilder => tableBuilder.ExcludeFromMigrations());

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.DocType).HasColumnName("doc_type").IsRequired();
        builder.Property(d => d.ApplicableTo).HasColumnName("applicable_to").IsRequired();
        builder.Property(d => d.Version).HasColumnName("version").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").IsRequired();
        builder.Property(d => d.IsActive).HasColumnName("is_active").HasDefaultValue(false).IsRequired();
        builder.Property(d => d.Required).HasColumnName("required").HasDefaultValue(false).IsRequired();
        builder.Property(d => d.Title).HasColumnName("title").IsRequired();
    }
}
