using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class CredentialUpdateSagaStateConfiguration : IEntityTypeConfiguration<CredentialUpdateSagaState>
{
    public void Configure(EntityTypeBuilder<CredentialUpdateSagaState> builder)
    {
        builder.ToTable("credential_update_saga_state", "identity");

        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.CurrentState).HasColumnName("current_state").IsRequired();

        builder.Property(x => x.UserUid).HasColumnName("user_uid").IsRequired();
        builder.Property(x => x.UserType).HasColumnName("user_type").IsRequired();
        builder.Property(x => x.ExternalId).HasColumnName("external_id").IsRequired();

        builder.Property(x => x.CredentialType).HasColumnName("credential_type").IsRequired();
        builder.Property(x => x.NewValue).HasColumnName("new_value").IsRequired();
        builder.Property(x => x.NewHash).HasColumnName("new_hash").IsRequired(false);
        builder.Property(x => x.OldValue).HasColumnName("old_value").IsRequired();
        builder.Property(x => x.OldHash).HasColumnName("old_hash").IsRequired(false);

        builder.Property(x => x.InitiatedBy).HasColumnName("initiated_by").IsRequired();
        builder.Property(x => x.NotificationEmail).HasColumnName("notification_email").IsRequired();
        builder.Property(x => x.NotificationFullName).HasColumnName("notification_full_name").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").IsRequired(false);

        builder.HasIndex(x => x.UserUid).HasDatabaseName("ix_credential_update_saga_state_user_uid");
        builder.HasIndex(x => x.CurrentState).HasDatabaseName("ix_credential_update_saga_state_current_state");
    }
}
