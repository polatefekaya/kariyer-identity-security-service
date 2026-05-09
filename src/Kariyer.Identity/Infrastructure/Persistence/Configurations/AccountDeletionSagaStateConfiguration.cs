using Kariyer.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Identity.Infrastructure.Persistence.Configurations;

public class AccountDeletionSagaStateConfiguration : IEntityTypeConfiguration<AccountDeletionSagaState>
{
    public void Configure(EntityTypeBuilder<AccountDeletionSagaState> builder)
    {
        builder.ToTable("account_deletion_saga_state", "identity");

        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.CurrentState).HasColumnName("current_state").IsRequired();

        builder.Property(x => x.UserUid).HasColumnName("user_uid").IsRequired();
        builder.Property(x => x.UserType).HasColumnName("user_type").IsRequired();
        builder.Property(x => x.ExternalId).HasColumnName("external_id").IsRequired();

        builder.Property(x => x.InitiatedBy).HasColumnName("initiated_by").IsRequired();
        builder.Property(x => x.InitiatedByUid).HasColumnName("initiated_by_uid").IsRequired(false);

        builder.Property(x => x.GracePeriodEndsAt).HasColumnName("grace_period_ends_at").IsRequired(false);
        builder.Property(x => x.SupabaseBannedAt).HasColumnName("supabase_banned_at").IsRequired(false);
        builder.Property(x => x.ExecutedAt).HasColumnName("executed_at").IsRequired(false);
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at").IsRequired(false);
        builder.Property(x => x.CancelledByUid).HasColumnName("cancelled_by_uid").IsRequired(false);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.UserUid).HasDatabaseName("ix_account_deletion_saga_state_user_uid");
        builder.HasIndex(x => new { x.CurrentState, x.GracePeriodEndsAt })
            .HasDatabaseName("ix_account_deletion_saga_state_grace_period");

        // DB-level guard: only one non-terminal deletion saga per user at a time.
        // Partial index — covers DeletionRequested, GracePeriodActive, Executing, Cancelling.
        builder.HasIndex(x => x.UserUid)
            .HasDatabaseName("uix_account_deletion_saga_state_active_user")
            .HasFilter("current_state NOT IN ('Deleted', 'Cancelled')")
            .IsUnique();
    }
}
