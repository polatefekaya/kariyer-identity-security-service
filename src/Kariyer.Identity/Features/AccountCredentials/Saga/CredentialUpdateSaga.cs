using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit;
using System.Diagnostics;

namespace Kariyer.Identity.Features.AccountCredentials.Saga;

public class CredentialUpdateSaga : MassTransitStateMachine<CredentialUpdateSagaState>
{
    public State AwaitingSupabase { get; private set; } = null!;
    public State SupabaseUpdated { get; private set; } = null!;
    public State Compensating { get; private set; } = null!;
    public State Compensated { get; private set; } = null!;

    public Event<InitiateCredentialSupabaseUpdateCommand> InitiateUpdateReceived { get; private set; } = null!;
    public Event<CredentialSupabaseUpdatedEvent> SupabaseUpdatedReceived { get; private set; } = null!;
    public Event<CredentialSupabaseUpdateFailedEvent> SupabaseUpdateFailedReceived { get; private set; } = null!;
    public Event<CredentialDbRevertedEvent> DbRevertedReceived { get; private set; } = null!;

    public CredentialUpdateSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => InitiateUpdateReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => SupabaseUpdatedReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => SupabaseUpdateFailedReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => DbRevertedReceived,
            x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Initially(
            When(InitiateUpdateReceived)
                .Then(ctx =>
                {
                    ctx.Saga.UserUid = ctx.Message.UserUid;
                    ctx.Saga.UserType = ctx.Message.UserType;
                    ctx.Saga.ExternalId = ctx.Message.ExternalId;
                    ctx.Saga.CredentialType = ctx.Message.CredentialType;
                    ctx.Saga.NewValue = ctx.Message.NewValue;
                    ctx.Saga.NewHash = ctx.Message.NewHash;
                    ctx.Saga.OldValue = ctx.Message.OldValue;
                    ctx.Saga.OldHash = ctx.Message.OldHash;
                    ctx.Saga.InitiatedBy = ctx.Message.InitiatedBy;
                    ctx.Saga.NotificationEmail = ctx.Message.NotificationEmail;
                    ctx.Saga.NotificationFullName = ctx.Message.NotificationFullName;
                    ctx.Saga.CreatedAt = DateTimeOffset.UtcNow;

                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("CredentialUpdateSaga.Initiated");
                    activity?.SetTag("saga.correlation_id", ctx.Saga.CorrelationId.ToString());
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    activity?.SetTag("credential.type", ctx.Saga.CredentialType);
                })
                .PublishAsync(ctx => ctx.Init<UpdateCredentialInSupabaseCommand>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.ExternalId,
                    ctx.Saga.CredentialType,
                    NewValue = ctx.Saga.NewValue
                }))
                .TransitionTo(AwaitingSupabase)
        );

        During(AwaitingSupabase,
            When(SupabaseUpdatedReceived)
                .Then(ctx =>
                {
                    ctx.Saga.CompletedAt = DateTimeOffset.UtcNow;

                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("CredentialUpdateSaga.SupabaseUpdated");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    activity?.SetTag("credential.type", ctx.Saga.CredentialType);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", $"credential_{ctx.Saga.CredentialType}_supabase_synced"),
                        new KeyValuePair<string, object?>("initiated_by", ctx.Saga.InitiatedBy));
                })
                .IfElse(
                    ctx => ctx.Saga.CredentialType == "email",
                    emailBranch => emailBranch
                        .PublishAsync(ctx => ctx.Init<AccountEmailChangedEvent>(new
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Uid = ctx.Saga.UserUid,
                            OldEmail = ctx.Saga.OldValue,
                            NewEmail = ctx.Saga.NewValue,
                            Email = ctx.Saga.NotificationEmail,
                            FullName = ctx.Saga.NotificationFullName
                        })),
                    phoneBranch => phoneBranch
                        .PublishAsync(ctx => ctx.Init<AccountPhoneChangedEvent>(new
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Uid = ctx.Saga.UserUid,
                            Email = ctx.Saga.NotificationEmail,
                            FullName = ctx.Saga.NotificationFullName,
                            NewPhone = ctx.Saga.NewValue
                        }))
                )
                .TransitionTo(SupabaseUpdated),

            When(SupabaseUpdateFailedReceived)
                .Then(ctx =>
                {
                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("CredentialUpdateSaga.SupabaseFailed");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    activity?.SetTag("credential.type", ctx.Saga.CredentialType);
                    activity?.SetStatus(ActivityStatusCode.Error, "Supabase credential update failed — initiating DB compensation.");
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", $"credential_{ctx.Saga.CredentialType}_supabase_failed"));
                })
                .PublishAsync(ctx => ctx.Init<RevertCredentialInDbCommand>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.UserUid,
                    ctx.Saga.UserType,
                    ctx.Saga.CredentialType,
                    OldValue = ctx.Saga.OldValue,
                    OldHash = ctx.Saga.OldHash
                }))
                .TransitionTo(Compensating)
        );

        During(Compensating,
            When(DbRevertedReceived)
                .Then(ctx =>
                {
                    ctx.Saga.CompletedAt = DateTimeOffset.UtcNow;

                    using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("CredentialUpdateSaga.Compensated");
                    activity?.SetTag("account.uid", ctx.Saga.UserUid);
                    activity?.SetTag("credential.type", ctx.Saga.CredentialType);
                    IdentityDiagnostics.AccountLifecycleCounter.Add(1,
                        new KeyValuePair<string, object?>("operation", $"credential_{ctx.Saga.CredentialType}_compensated"));
                })
                .TransitionTo(Compensated)
        );

        During(SupabaseUpdated, Compensated,
            Ignore(SupabaseUpdatedReceived),
            Ignore(SupabaseUpdateFailedReceived),
            Ignore(DbRevertedReceived));
    }
}
