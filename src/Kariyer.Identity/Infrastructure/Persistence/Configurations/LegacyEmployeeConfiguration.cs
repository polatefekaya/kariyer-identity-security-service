using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class LegacyEmployeeConfiguration : IEntityTypeConfiguration<LegacyEmployee>
{
    public void Configure(EntityTypeBuilder<LegacyEmployee> builder)
    {
        builder.ToTable("employee");
        
        builder.HasKey(e => e.Uid);

        builder.Property(e => e.Uid).HasColumnName("uid").IsRequired();
        builder.Property(e => e.ExternalId).HasColumnName("external_id").IsRequired(false); 
        builder.Property(e => e.IsAccountCompleted).HasColumnName("is_account_completed").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.Property(e => e.Phone).HasColumnName("phone").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.Surname).HasColumnName("surname").IsRequired();
        builder.Property(e => e.Username).HasColumnName("username").IsRequired();
        builder.Property(e => e.Password).HasColumnName("password").IsRequired();
        builder.Property(e => e.BirthDate).HasColumnName("birth_date").IsRequired();
        
        builder.Property(e => e.SelectedSkills).HasColumnName("selected_skills").HasColumnType("text[]");
        builder.Property(e => e.Following).HasColumnName("following").HasColumnType("text[]");
        builder.Property(e => e.Followers).HasColumnName("followers").HasColumnType("text[]");
        builder.Property(e => e.Notifications).HasColumnName("notifications").HasColumnType("text[]");
        builder.Property(e => e.Jwt).HasColumnName("jwt").HasColumnType("text[]");
    }
}