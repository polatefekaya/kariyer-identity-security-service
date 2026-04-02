using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegacyAdminConfiguration : IEntityTypeConfiguration<LegacyAdmin>
{
    public void Configure(EntityTypeBuilder<LegacyAdmin> builder)
    {
        builder.ToTable("admin");

        builder.HasKey(a => a.Uid);

        builder.Property(a => a.Uid).HasColumnName("uid").IsRequired();
        builder.Property(a => a.ExternalId).HasColumnName("external_id").IsRequired(false);
        builder.Property(a => a.IsAccountCompleted).HasColumnName("is_account_completed").HasDefaultValue(false);

        builder.Property(a => a.Email).HasColumnName("email");
        builder.Property(a => a.Username).HasColumnName("username");
        builder.Property(a => a.Name).HasColumnName("name");
        builder.Property(a => a.Surname).HasColumnName("surname");
        builder.Property(a => a.Phone).HasColumnName("phone");
        builder.Property(a => a.Race).HasColumnName("race");
        builder.Property(a => a.BirthDate).HasColumnName("birth_date");
        builder.Property(a => a.Country).HasColumnName("country");
        builder.Property(a => a.Province).HasColumnName("province");
        builder.Property(a => a.Town).HasColumnName("town");
        builder.Property(a => a.Neighbourhood).HasColumnName("neighbourhood");
        
        builder.Property(a => a.NationalIdHash).HasColumnName("national_id_hash");
        builder.Property(a => a.PasswordHash).HasColumnName("password_hash");
        builder.Property(a => a.EmailHash).HasColumnName("email_hash");
        builder.Property(a => a.PhoneHash).HasColumnName("phone_hash");

        builder.Property(a => a.PhotoUrl).HasColumnName("photo_url").HasDefaultValue("");
        builder.Property(a => a.BackgroundUrl).HasColumnName("background_url").HasDefaultValue("");
        builder.Property(a => a.Gender).HasColumnName("gender").HasDefaultValue("Erkek");
        builder.Property(a => a.Address).HasColumnName("address").HasColumnType("text").HasDefaultValue("");
        builder.Property(a => a.Title).HasColumnName("title").HasDefaultValue("");
        builder.Property(a => a.AdminRole).HasColumnName("admin_role").HasDefaultValue("admin");

        builder.Property(a => a.Notifications).HasColumnName("notifications").HasColumnType("text[]").HasDefaultValue(Array.Empty<string>());
        builder.Property(a => a.Jwt).HasColumnName("jwt").HasColumnType("text[]").HasDefaultValue(Array.Empty<string>());

        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(a => a.PermaDeleted).HasColumnName("perma_deleted").HasDefaultValue(false);
        builder.Property(a => a.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(a => a.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false);
        builder.Property(a => a.PhoneVerified).HasColumnName("phone_verified").HasDefaultValue(false);

        builder.Property(a => a.CreatedDate).HasColumnName("created_date").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.LastLogin).HasColumnName("last_login");
        builder.Property(a => a.EmailUpdate).HasColumnName("email_update");
        builder.Property(a => a.PhoneUpdate).HasColumnName("phone_update");
        builder.Property(a => a.UsernameUpdate).HasColumnName("username_update");
        builder.Property(a => a.PasswordUpdate).HasColumnName("password_update");
        
        builder.Property(a => a.VerificationCode).HasColumnName("verification_code").HasDefaultValue("");
        builder.Property(a => a.VerificationCodeExpiresAt).HasColumnName("verification_code_expires_at");
        builder.Property(a => a.PasswordResetToken).HasColumnName("password_reset_token");
        builder.Property(a => a.PasswordResetExpiresAt).HasColumnName("password_reset_expires_at");

        builder.HasIndex(a => a.Email).IsUnique();
        builder.HasIndex(a => a.ExternalId).IsUnique();
    }
}