using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Kariyer.Identity.Infrastructure.Telemetry;
using Microsoft.Extensions.Primitives;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public class SupabaseDatabaseWebhookFilter(IConfiguration config, ILogger<SupabaseDatabaseWebhookFilter> logger) : IEndpointFilter
{
    private readonly string _webhookSecret = config["ExternalProvider:DatabaseWebhookSecret"]
        ?? throw new ArgumentNullException("CRITICAL: ExternalProvider:DatabaseWebhookSecret is missing from configuration.");

    private readonly ILogger<SupabaseDatabaseWebhookFilter> _logger = logger;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpRequest request = context.HttpContext.Request;

        // Emit a span for the authorization step. The downstream handler only starts its own
        // span AFTER this filter passes, so a 401 here used to be invisible in the traces view —
        // a misconfigured X-Webhook-Secret looked exactly like "the webhook never fired", which
        // is the usual reason registration creates nothing. Now every rejection is a named span
        // with an error status + reason tag, and is counted by WebhookRejectedCounter.
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("Supabase.DatabaseWebhook.Authorize");

        if (!request.Headers.TryGetValue("X-Webhook-Secret", out StringValues providedSecretValues))
        {
            _logger.LogWarning("Blocked database webhook request: Missing 'X-Webhook-Secret' header.");
            activity?.SetTag("webhook.auth.outcome", "missing_secret");
            activity?.SetStatus(ActivityStatusCode.Error, "Missing X-Webhook-Secret header");
            IdentityDiagnostics.WebhookRejectedCounter.Add(1, new KeyValuePair<string, object?>("reason", "missing_secret"));
            return Results.Unauthorized();
        }

        string providedSecret = providedSecretValues.ToString();

        byte[] expectedBytes = Encoding.UTF8.GetBytes(_webhookSecret);
        byte[] providedBytes = Encoding.UTF8.GetBytes(providedSecret);

        if (expectedBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            _logger.LogWarning("Blocked database webhook request: Invalid 'X-Webhook-Secret' value.");
            activity?.SetTag("webhook.auth.outcome", "invalid_secret");
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid X-Webhook-Secret value");
            IdentityDiagnostics.WebhookRejectedCounter.Add(1, new KeyValuePair<string, object?>("reason", "invalid_secret"));
            return Results.Unauthorized();
        }

        activity?.SetTag("webhook.auth.outcome", "authorized");
        return await next(context);
    }
}