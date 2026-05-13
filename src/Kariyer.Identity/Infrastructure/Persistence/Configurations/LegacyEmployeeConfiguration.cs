using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegacyEmployeeConfiguration : IEntityTypeConfiguration<LegacyEmployee>
{
    public void Configure(EntityTypeBuilder<LegacyEmployee> builder)
    {
        builder.ToTable("employee", tableBuilder => 
        {
            tableBuilder.ExcludeFromMigrations();
        });
        
        builder.HasKey(e => e.Uid);

        // Primary Keys & Identifiers
        builder.Property(e => e.Uid).HasColumnName("uid").IsRequired();
        builder.Property(e => e.ExternalId).HasColumnName("external_id").IsRequired(false); 
        
        // Booleans
        builder.Property(e => e.IsAccountCompleted).HasColumnName("is_account_completed").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.PermaDeleted).HasColumnName("perma_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.IsRating).HasColumnName("is_rating").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.PhoneVerified).HasColumnName("phone_verified").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CompanyEmailVerified).HasColumnName("company_email_verified").HasDefaultValue(false).IsRequired();

        builder.Property(e => e.UserAgreementAccepted).HasColumnName("user_agreement_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.KvkkAccepted).HasColumnName("kvkk_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CommercialConsentAccepted).HasColumnName("commercial_consent_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.AcikRizaDataAbroadAccepted).HasColumnName("acik_riza_data_abroad_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.AcikRizaHealthDataAccepted).HasColumnName("acik_riza_health_data_accepted").HasDefaultValue(false).IsRequired();
        

        // Strings (allowNull: false)
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.Property(e => e.LookingJob).HasColumnName("looking_job").HasDefaultValue("0").IsRequired();

        // Strings (allowNull: true - The fix for the crash)
        builder.Property(e => e.Username).HasColumnName("username").IsRequired(false);
        builder.Property(e => e.PhotoUrl).HasColumnName("photo_url").IsRequired(false);
        builder.Property(e => e.BackgroundUrl).HasColumnName("background_url").IsRequired(false);
        builder.Property(e => e.Name).HasColumnName("name").IsRequired(false);
        builder.Property(e => e.Surname).HasColumnName("surname").IsRequired(false);
        builder.Property(e => e.Password).HasColumnName("password").IsRequired(false);
        builder.Property(e => e.Phone).HasColumnName("phone").IsRequired(false);
        builder.Property(e => e.Race).HasColumnName("race").IsRequired(false);
        builder.Property(e => e.NationalId).HasColumnName("national_id").IsRequired(false);
        builder.Property(e => e.Gender).HasColumnName("gender").IsRequired(false);
        builder.Property(e => e.Country).HasColumnName("country").IsRequired(false);
        builder.Property(e => e.Province).HasColumnName("province").IsRequired(false);
        builder.Property(e => e.Town).HasColumnName("town").IsRequired(false);
        builder.Property(e => e.Adress).HasColumnName("adress").IsRequired(false);
        builder.Property(e => e.Describe).HasColumnName("describe").IsRequired(false);
        builder.Property(e => e.Title).HasColumnName("title").IsRequired(false);
        builder.Property(e => e.WorkingType).HasColumnName("working_type").IsRequired(false);
        builder.Property(e => e.Neighbourhood).HasColumnName("neighbourhood").IsRequired(false);
        builder.Property(e => e.CompanyEmail).HasColumnName("company_email").IsRequired(false);
        builder.Property(e => e.VerificationCode).HasColumnName("verification_code").IsRequired(false);
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by").IsRequired(false);
        builder.Property(e => e.OnesignalPlayerId).HasColumnName("onesignal_player_id").HasMaxLength(255).IsRequired(false);

        // Hashes (allowNull: true)
        builder.Property(e => e.EmailHash).HasColumnName("email_hash").IsRequired(false);
        builder.Property(e => e.NationalIdHash).HasColumnName("national_id_hash").IsRequired(false);
        builder.Property(e => e.PhoneHash).HasColumnName("phone_hash").IsRequired(false);
        builder.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired(false);

        // Dates
        builder.Property(e => e.CreatedDate).HasColumnName("created_date").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(e => e.BirthDate).HasColumnName("birth_date").IsRequired(false);
        builder.Property(e => e.EmailUpdate).HasColumnName("email_update").IsRequired(false);
        builder.Property(e => e.PhoneUpdate).HasColumnName("phone_update").IsRequired(false);
        builder.Property(e => e.UsernameUpdate).HasColumnName("username_update").IsRequired(false);
        builder.Property(e => e.VerificationCodeExpiresAt).HasColumnName("verification_code_expires_at").IsRequired(false);
        builder.Property(e => e.CompanyEmailOtpExpiredAt).HasColumnName("company_email_otp_expired_at").IsRequired(false);
        builder.Property(e => e.PasswordResetLastAttempt).HasColumnName("password_reset_last_attempt").IsRequired(false);
        builder.Property(e => e.PasswordResetBlockedUntil).HasColumnName("password_reset_blocked_until").IsRequired(false);
        builder.Property(e => e.DeletedDate).HasColumnName("deleted_date").IsRequired(false);
        builder.Property(e => e.DeletionRequestedAt).HasColumnName("deletion_requested_at").IsRequired(false);

        // Integers
        builder.Property(e => e.OnboardingReminderStep).HasColumnName("onboarding_reminder_step").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.CompanyEmailOtpCount).HasColumnName("company_email_otp_count").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.PasswordResetCount).HasColumnName("password_reset_count").HasDefaultValue(0).IsRequired();

        // Arrays
        builder.Property(e => e.SelectedSkills).HasColumnName("selected_skills").HasColumnType("text[]");
        builder.Property(e => e.Following).HasColumnName("following").HasColumnType("text[]");
        builder.Property(e => e.Followers).HasColumnName("followers").HasColumnType("text[]");
        builder.Property(e => e.Notifications).HasColumnName("notifications").HasColumnType("text[]");
        builder.Property(e => e.Jwt).HasColumnName("jwt").HasColumnType("text[]");
    }
}