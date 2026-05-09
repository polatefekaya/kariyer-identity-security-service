using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountLifecycle.Saga;

public class AccountDeletionSaga : MassTransitStateMachine<AccountDeletionSagaState>
{
    public State DeletionRequested { get; private set; } = null!;
    public State GracePeriodActive { get; private set; } = null!;
    public State Executing { get; private set; } = null!;
    public State Deleted { get; private set; } = null!;
    public State Cancelling { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<RequestAccountDeletionCommand> RequestAccountDeletionReceived { get; private set; } = null!;
    public Event<UserBannedForDeletionEvent> UserBannedForDeletionReceived { get; private set; } = null!;
    public Event<ExecuteAccountDeletionCommand> ExecuteAccountDeletionReceived { get; private set; } = null!;
    public Event<CancelAccountDeletionCommand> CancelAccountDeletionReceived { get; private set; } = null!;
    public Event<UserUnbannedEvent> UserUnbannedReceived { get; private set; } = null!;
    public Event<UserPermanentlyDeletedEvent> UserPermanentlyDeletedReceived { get; private set; } = null!;

    public AccountDeletionSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => RequestAccountDeletionReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => UserBannedForDeletionReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => UserPermanentlyDeletedReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => UserUnbannedReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        // Correlated by UserUid since sweeper and admin don't know CorrelationId
        Event(() => ExecuteAccountDeletionReceived,
            x => x.CorrelateBy(saga => saga.UserUid, ctx => ctx.Message.UserUid)
                  .OnMissingInstance(m => m.Discard()));

        Event(() => CancelAccountDeletionReceived,
            x => x.CorrelateBy(saga => saga.UserUid, ctx => ctx.Message.UserUid)
                  .OnMissingInstance(m => m.Discard()));

        Initially(
            When(RequestAccountDeletionReceived)
                .Then(ctx =>
                {
                    ctx.Saga.UserUid = ctx.Message.UserUid;
                    ctx.Saga.UserType = ctx.Message.UserType;
                    ctx.Saga.ExternalId = ctx.Message.ExternalId;
                    ctx.Saga.InitiatedBy = ctx.Message.InitiatedBy;
                    ctx.Saga.InitiatedByUid = ctx.Message.InitiatedByUid;
                    ctx.Saga.CreatedAt = DateTimeOffset.UtcNow;

                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.DeletionRequested");
                    activity?.SetTag("saga.correlation_id", ctx.Saga.CorrelationId.ToString());
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    activity?.SetTag("account.type", ctx.Saga.UserType);
                    activity?.SetTag("deletion.initiated_by", ctx.Saga.InitiatedBy);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", "deletion_requested"),
                        new KeyValuePair<string, object?>("initiated_by", ctx.Saga.InitiatedBy));
                })
                .PublishAsync(ctx => ctx.Init<BanUserForDeletionCommand>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.UserUid,
                    ctx.Saga.ExternalId
                }))
                .TransitionTo(DeletionRequested)
        );

        During(DeletionRequested,
            When(UserBannedForDeletionReceived)
                .Then(ctx => ctx.Saga.SupabaseBannedAt = DateTimeOffset.UtcNow)
                .IfElse(
                    ctx => ctx.Saga.InitiatedBy == "admin",
                    admin => admin
                        .Then(ctx =>
                        {
                            using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.AdminImmediateExecution");
                            activity?.SetTag("account.uid", ctx.Saga.UserUid);
                        })
                        .PublishAsync(ctx => ctx.Init<DeleteUserPermanentlyCommand>(new
                        {
                            CorrelationId = ctx.Saga.CorrelationId,
                            ctx.Saga.UserUid,
                            ctx.Saga.UserType,
                            ctx.Saga.ExternalId
                        }))
                        .TransitionTo(Executing),
                    self => self
                        .Then(ctx =>
                        {
                            ctx.Saga.GracePeriodEndsAt = DateTimeOffset.UtcNow.AddDays(30);
                            using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.GracePeriodStarted");
                            activity?.SetTag("account.uid", ctx.Saga.UserUid);
                            activity?.SetTag("grace_period.ends_at", ctx.Saga.GracePeriodEndsAt.ToString());
                        })
                        .TransitionTo(GracePeriodActive)
                )
        );

        During(GracePeriodActive,
            When(ExecuteAccountDeletionReceived)
                .Then(ctx =>
                {
                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.GracePeriodExpired");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", "grace_period_expired"));
                })
                .PublishAsync(ctx => ctx.Init<DeleteUserPermanentlyCommand>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.UserUid,
                    ctx.Saga.UserType,
                    ctx.Saga.ExternalId
                }))
                .TransitionTo(Executing),

            When(CancelAccountDeletionReceived)
                .Then(ctx =>
                {
                    ctx.Saga.CancelledAt = DateTimeOffset.UtcNow;
                    ctx.Saga.CancelledByUid = ctx.Message.CancelledByUid;

                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.Cancelling");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    activity?.SetTag("cancelled_by", ctx.Saga.CancelledByUid);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", "deletion_cancelling"));
                })
                .PublishAsync(ctx => ctx.Init<UnbanUserCommand>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.UserUid,
                    ctx.Saga.ExternalId
                }))
                .TransitionTo(Cancelling)
        );

        During(Executing,
            When(UserPermanentlyDeletedReceived)
                .Then(ctx =>
                {
                    ctx.Saga.ExecutedAt = DateTimeOffset.UtcNow;

                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.Deleted");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", "deletion_executed"));
                })
                .TransitionTo(Deleted)
        );

        During(Cancelling,
            When(UserUnbannedReceived)
                .Then(ctx =>
                {
                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("AccountDeletionSaga.Cancelled");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", "deletion_cancelled"));
                })
                .PublishAsync(ctx => ctx.Init<AccountDeletionCancelledEvent>(new
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Uid = ctx.Saga.UserUid,
                    ctx.Message.Email,
                    ctx.Message.FullName,
                    CancelledByUid = ctx.Saga.CancelledByUid ?? string.Empty
                }))
                .TransitionTo(Cancelled)
        );

        // Discard any redelivered saga events that arrive after the saga has reached a terminal state.
        // Without these, MassTransit raises UnhandledEventException on retry/redelivery.
        During(Deleted, Cancelled,
            Ignore(UserBannedForDeletionReceived),
            Ignore(UserPermanentlyDeletedReceived),
            Ignore(UserUnbannedReceived),
            Ignore(ExecuteAccountDeletionReceived),
            Ignore(CancelAccountDeletionReceived));
    }
}
