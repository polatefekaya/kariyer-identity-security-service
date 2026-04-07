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
}