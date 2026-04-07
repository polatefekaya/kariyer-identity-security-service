using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kariyer.Identity.Infrastructure.Telemetry;

public static class IdentityDiagnostics
{
    public const string ServiceName = "Kariyer.Identity";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<int> OAuthMetadataSyncCounter = Meter.CreateCounter<int>(
        name: "identity.oauth.metadata_sync.count",
        unit: "{syncs}",
        description: "Counts the number of OAuth metadata backfill operations to Supabase.");
    
    public static readonly Counter<int> WebhookProcessedCounter = Meter.CreateCounter<int>(
        name: "identity.webhook.processed.count",
        unit: "{webhooks}",
        description: "Counts the number of processed Supabase webhooks and their outcomes.");
}