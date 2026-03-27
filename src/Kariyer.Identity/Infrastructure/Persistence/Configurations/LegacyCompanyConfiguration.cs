using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegacyCompanyConfiguration : IEntityTypeConfiguration<LegacyCompany>
{
    public void Configure(EntityTypeBuilder<LegacyCompany> builder)
    {
        builder.ToTable("company");
        
        builder.HasKey(c => c.Uid);

        builder.Property(c => c.Uid).HasColumnName("uid").IsRequired();
        builder.Property(e => e.ExternalId).HasColumnName("external_id").IsRequired(false); 
        builder.Property(e => e.IsAccountCompleted).HasColumnName("is_account_completed").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").IsRequired();
        builder.Property(c => c.Phone).HasColumnName("phone").IsRequired();
        builder.Property(c => c.CompanyName).HasColumnName("company_name").IsRequired();
        builder.Property(c => c.AuthorizedName).HasColumnName("authorized_name").IsRequired();
        builder.Property(c => c.AuthorizedSurname).HasColumnName("authorized_surname").IsRequired();
        builder.Property(c => c.Username).HasColumnName("username").IsRequired();
        builder.Property(c => c.Password).HasColumnName("password").IsRequired();

        builder.Property(c => c.Followers).HasColumnName("followers").HasColumnType("text[]");
        builder.Property(c => c.SubCompanies).HasColumnName("sub_companies").HasColumnType("text[]");
        builder.Property(c => c.ParentCompanies).HasColumnName("parent_companies").HasColumnType("text[]");
        builder.Property(c => c.AffiliatedCompanies).HasColumnName("affiliated_companies").HasColumnType("text[]");
        builder.Property(c => c.PendingSentRequests).HasColumnName("pending_sent_requests").HasColumnType("text[]");
        builder.Property(c => c.PendingReceivedRequests).HasColumnName("pending_received_requests").HasColumnType("text[]");
        builder.Property(c => c.Employees).HasColumnName("employees").HasColumnType("text[]");
        builder.Property(c => c.ApprovedEmployees).HasColumnName("approved_employees").HasColumnType("text[]");
        builder.Property(c => c.RejectedEmployees).HasColumnName("rejected_employees").HasColumnType("text[]");
        builder.Property(c => c.DeletedEmployees).HasColumnName("deleted_employees").HasColumnType("text[]");
        builder.Property(c => c.Jwt).HasColumnName("jwt").HasColumnType("text[]");
        builder.Property(c => c.Plan).HasColumnName("plan").HasColumnType("text[]");
        builder.Property(c => c.Tags).HasColumnName("tags").HasColumnType("text[]");
        builder.Property(c => c.Notifications).HasColumnName("notifications").HasColumnType("text[]");
     
        builder.Property(c => c.SeenCv).HasColumnName("seen_cv").HasColumnType("integer[]");
        builder.Property(c => c.DownloadedCv).HasColumnName("downloaded_cv").HasColumnType("integer[]");
    }
}