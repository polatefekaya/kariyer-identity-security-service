using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegacyCompanyConfiguration : IEntityTypeConfiguration<LegacyCompany>
{
    public void Configure(EntityTypeBuilder<LegacyCompany> builder)
    {
        builder.ToTable("company", tableBuilder => 
        {
            tableBuilder.ExcludeFromMigrations();
        });
        
        builder.HasKey(c => c.Uid);

        // Core Identifiers
        builder.Property(c => c.Uid).HasColumnName("uid").IsRequired();
        builder.Property(c => c.ExternalId).HasColumnName("external_id").IsRequired(false); 
        builder.Property(c => c.IsAccountCompleted).HasColumnName("is_account_completed").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").IsRequired();

        // THE FATAL CRASH FIX: These MUST be IsRequired(false) to match Sequelize allowNull: true
        builder.Property(c => c.Phone).HasColumnName("phone").IsRequired(false);
        builder.Property(c => c.CompanyName).HasColumnName("company_name").IsRequired(false);
        builder.Property(c => c.AuthorizedName).HasColumnName("authorized_name").IsRequired(false);
        builder.Property(c => c.AuthorizedSurname).HasColumnName("authorized_surname").IsRequired(false);
        builder.Property(c => c.Username).HasColumnName("username").IsRequired(false);
        builder.Property(c => c.Password).HasColumnName("password").IsRequired(false);
        builder.Property(c => c.Status).HasColumnName("status").IsRequired(false);

        // Onboarding Form Fields (Missing from your original config)
        builder.Property(c => c.Country).HasColumnName("country").IsRequired(false);
        builder.Property(c => c.Province).HasColumnName("province").IsRequired(false);
        builder.Property(c => c.Town).HasColumnName("town").IsRequired(false);
        builder.Property(c => c.Neighbourhood).HasColumnName("neighbourhood").IsRequired(false);
        builder.Property(c => c.Location).HasColumnName("location").IsRequired(false);
        builder.Property(c => c.TaxOfficeProvince).HasColumnName("tax_office_province").IsRequired(false);
        builder.Property(c => c.TaxOffice).HasColumnName("tax_office").IsRequired(false);
        builder.Property(c => c.TaxIdNumber).HasColumnName("tax_id_number").IsRequired(false);
        builder.Property(c => c.MailDomain).HasColumnName("mail_domain").IsRequired(false);
        builder.Property(c => c.FoundedYear).HasColumnName("founded_year").IsRequired(false);
        builder.Property(c => c.CompanyWebsite).HasColumnName("company_website").IsRequired(false);
        builder.Property(c => c.Industry).HasColumnName("industry").IsRequired(false);
        builder.Property(c => c.CompanyDesc).HasColumnName("company_desc").IsRequired(false);
        builder.Property(c => c.PhotoUrl).HasColumnName("photo_url").IsRequired(false);
        builder.Property(c => c.BackgroundUrl).HasColumnName("background_url").IsRequired(false);
        builder.Property(c => c.TaxCertificateUrl).HasColumnName("tax_certificate_url").IsRequired(false);
        builder.Property(c => c.EmployeeCount).HasColumnName("employee_count").IsRequired(false);

        // Booleans
        builder.Property(c => c.IsCivilian).HasColumnName("is_civilian").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.EmploymentOffice).HasColumnName("employment_office").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.PermaDeleted).HasColumnName("perma_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.PhoneVerified).HasColumnName("phone_verified").HasDefaultValue(false).IsRequired();

        builder.Property(c => c.UserAgreementAccepted).HasColumnName("user_agreement_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.KvkkAccepted).HasColumnName("kvkk_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.CommercialConsentAccepted).HasColumnName("commercial_consent_accepted").HasDefaultValue(false).IsRequired();

        // Integers
        builder.Property(c => c.PriorityScore).HasColumnName("priority_score").HasDefaultValue(0).IsRequired();
        builder.Property(c => c.PasswordResetCount).HasColumnName("password_reset_count").HasDefaultValue(0).IsRequired();
        builder.Property(c => c.OnboardingReminderStep).HasColumnName("onboarding_reminder_step").HasDefaultValue(0).IsRequired();

        // Arrays (Mapped natively to Postgres arrays)
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

        // Hashes & Socials
        builder.Property(c => c.TaxIdNumberHash).HasColumnName("tax_id_number_hash").IsRequired(false);
        builder.Property(c => c.EmailHash).HasColumnName("email_hash").IsRequired(false);
        builder.Property(c => c.PhoneHash).HasColumnName("phone_hash").IsRequired(false);
        builder.Property(c => c.PasswordHash).HasColumnName("password_hash").IsRequired(false);
        builder.Property(c => c.Instagram).HasColumnName("instagram").IsRequired(false);
        builder.Property(c => c.InstagramUrl).HasColumnName("instagram_url").IsRequired(false);
        builder.Property(c => c.Linkedin).HasColumnName("linkedin").IsRequired(false);
        builder.Property(c => c.LinkedinUrl).HasColumnName("linkedin_url").IsRequired(false);
        builder.Property(c => c.Twitter).HasColumnName("twitter").IsRequired(false);
        builder.Property(c => c.TwitterUrl).HasColumnName("twitter_url").IsRequired(false);

        // Dates & Extras
        builder.Property(c => c.VerificationCode).HasColumnName("verification_code").IsRequired(false);
        builder.Property(c => c.VerificationCodeExpiresAt).HasColumnName("verification_code_expires_at").IsRequired(false);
        builder.Property(c => c.PasswordResetLastAttempt).HasColumnName("password_reset_last_attempt").IsRequired(false);
        builder.Property(c => c.PasswordResetBlockedUntil).HasColumnName("password_reset_blocked_until").IsRequired(false);
        builder.Property(c => c.EmailUpdate).HasColumnName("email_update").IsRequired(false);
        builder.Property(c => c.PhoneUpdate).HasColumnName("phone_update").IsRequired(false);
        builder.Property(c => c.UsernameUpdate).HasColumnName("username_update").IsRequired(false);
        builder.Property(c => c.DeletedDate).HasColumnName("deleted_date").IsRequired(false);
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by").IsRequired(false);
        builder.Property(c => c.DeletionRequestedAt).HasColumnName("deletion_requested_at").IsRequired(false);
        builder.Property(c => c.OnesignalPlayerId).HasColumnName("onesignal_player_id").IsRequired(false);
        builder.Property(c => c.ApprovedBy).HasColumnName("approved_by").IsRequired(false);
        builder.Property(c => c.ApprovedAt).HasColumnName("approved_at").IsRequired(false);
        builder.Property(c => c.RejectedBy).HasColumnName("rejected_by").IsRequired(false);
        builder.Property(c => c.RejectedAt).HasColumnName("rejected_at").IsRequired(false);
        builder.Property(c => c.RejectionReason).HasColumnName("rejection_reason").IsRequired(false);

        builder.Property(c => c.Approved)
               .HasColumnName("approved")
               .HasDefaultValue(ApprovedStatus.Registered)
               .HasConversion(new ValueConverter<ApprovedStatus, string>(
                   v => v switch
                   {
                       ApprovedStatus.Inserted => "inserted",
                       ApprovedStatus.None     => "none",
                       _                       => "registered"
                   },
                   v => v switch
                   {
                       "inserted" => ApprovedStatus.Inserted,
                       "none"     => ApprovedStatus.None,
                       _          => ApprovedStatus.Registered
                   }))
               .IsRequired();

        builder.Property(c => c.CreatedDate)
               .HasColumnName("created_date")
               .HasDefaultValueSql("CURRENT_TIMESTAMP")
               .IsRequired();
    }
}