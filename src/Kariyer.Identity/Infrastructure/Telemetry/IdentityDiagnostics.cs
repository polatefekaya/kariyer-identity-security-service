using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kariyer.Identity.Infrastructure.Telemetry;

public static class IdentityDiagnostics
{
    public const string ServiceName = "Kariyer.Identity";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // ── Webhooks ──────────────────────────────────────────────────────────────

    public static readonly Counter<int> WebhookProcessedCounter = Meter.CreateCounter<int>(
        name: "identity.webhook.processed.count",
        unit: "{webhooks}",
        description: "Supabase webhook events processed, tagged by outcome (success/error) and account_type.");

    public static readonly Counter<int> AccountSyncedCounter = Meter.CreateCounter<int>(
        name: "identity.account.synced.count",
        unit: "{accounts}",
        description: "Accounts synced from Supabase webhook, tagged by account_type (employee/company).");

    public static readonly Counter<int> WebhookRejectedCounter = Meter.CreateCounter<int>(
        name: "identity.webhook.rejected.count",
        unit: "{webhooks}",
        description: "Supabase webhook requests rejected at the auth filter before any processing, tagged by reason (missing_secret/invalid_secret). A non-zero rate here is the usual cause of 'registration creates nothing' — the X-Webhook-Secret is misconfigured.");

    // ── Admin operations ──────────────────────────────────────────────────────

    public static readonly Counter<int> AdminOperationsCounter = Meter.CreateCounter<int>(
        name: "identity.admin.operations.count",
        unit: "{operations}",
        description: "Admin CRUD operations, tagged by operation (create/update/delete/status/role) and outcome.");

    public static readonly Histogram<double> AdminOperationDuration = Meter.CreateHistogram<double>(
        name: "identity.admin.operation.duration",
        unit: "ms",
        description: "Duration of admin operations in milliseconds, tagged by operation.");

    // ── Account lifecycle ─────────────────────────────────────────────────────

    public static readonly Counter<int> AccountLifecycleCounter = Meter.CreateCounter<int>(
        name: "identity.account.lifecycle.count",
        unit: "{operations}",
        description: "Account lifecycle operations (freeze/restore/deletion_requested/deletion_executed/deletion_cancelled).");

    public static readonly Histogram<double> AccountOperationDuration = Meter.CreateHistogram<double>(
        name: "identity.account.operation.duration",
        unit: "ms",
        description: "Duration of account lifecycle operations in milliseconds, tagged by operation.");

    // ── Credentials ───────────────────────────────────────────────────────────

    public static readonly Counter<int> CredentialUpdateCounter = Meter.CreateCounter<int>(
        name: "identity.credentials.update.count",
        unit: "{updates}",
        description: "Credential update operations, tagged by field (email/phone/username/password) and outcome.");

    public static readonly Histogram<double> CredentialOperationDuration = Meter.CreateHistogram<double>(
        name: "identity.credentials.operation.duration",
        unit: "ms",
        description: "Duration of credential update operations in milliseconds, tagged by field.");

    // ── Sagas ─────────────────────────────────────────────────────────────────

    public static readonly Counter<int> SagaTransitionCounter = Meter.CreateCounter<int>(
        name: "identity.saga.transitions.count",
        unit: "{transitions}",
        description: "Saga state machine transitions, tagged by saga (account_deletion/credential_update) and to_state.");

    // ── Auth ──────────────────────────────────────────────────────────────────

    public static readonly Counter<int> AuthValidationCounter = Meter.CreateCounter<int>(
        name: "identity.auth.validation.count",
        unit: "{attempts}",
        description: "JWT authentication validation attempts, tagged by outcome (success/failure) and account_type.");

    // ── Rate limiting ─────────────────────────────────────────────────────────

    public static readonly Counter<int> RateLimitRejectedCounter = Meter.CreateCounter<int>(
        name: "identity.rate_limit.rejected.count",
        unit: "{rejections}",
        description: "Requests rejected by rate limiter, tagged by policy (AccountLifecycle/AdminOperations).");

    // ── Reverse proxy ─────────────────────────────────────────────────────────

    public static readonly Counter<int> ProxyRequestCounter = Meter.CreateCounter<int>(
        name: "identity.proxy.requests.count",
        unit: "{requests}",
        description: "Requests forwarded by YARP reverse proxy, tagged by upstream (supabase/node-backend).");

    // ── Supabase admin API ────────────────────────────────────────────────────

    public static readonly Counter<int> SupabaseCallCounter = Meter.CreateCounter<int>(
        name: "identity.supabase.calls.count",
        unit: "{calls}",
        description: "Supabase Admin API calls, tagged by operation and outcome.");

    // ── Background sweepers ───────────────────────────────────────────────────

    public static readonly Counter<int> SweeperProcessedCounter = Meter.CreateCounter<int>(
        name: "identity.sweeper.processed.count",
        unit: "{accounts}",
        description: "Accounts processed by background sweeper workers, tagged by sweeper (incomplete/grace_period) and account_type.");

    public static readonly Histogram<double> SweeperBatchDuration = Meter.CreateHistogram<double>(
        name: "identity.sweeper.batch.duration",
        unit: "ms",
        description: "Duration of sweeper batch runs in milliseconds, tagged by sweeper type.");
}
