using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kariyer.Identity.Infrastructure.Telemetry;

public static class IdentityDiagnostics
{
    public const string ServiceName = "Kariyer.Identity";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<int> WebhookProcessedCounter = Meter.CreateCounter<int>(
        name: "identity.webhook.processed.count",
        unit: "{webhooks}",
        description: "Counts the number of processed Supabase webhooks and their outcomes.");
    
    public static readonly Counter<int> AdminOperationsCounter = Meter.CreateCounter<int>(
            name: "identity.admin.operations.count",
            unit: "{operations}",
            description: "Counts the number of admin operations (create, update, delete, status toggle).");

    public static readonly Counter<int> AccountLifecycleCounter = Meter.CreateCounter<int>(
        name: "identity.account.lifecycle.count",
        unit: "{operations}",
        description: "Counts account lifecycle operations (freeze, restore, deletion_requested, deletion_executed, deletion_cancelled).");
}