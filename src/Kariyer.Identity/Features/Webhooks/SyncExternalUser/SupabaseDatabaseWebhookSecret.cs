using System.Security.Cryptography;
using System.Text;
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
        
        if (!request.Headers.TryGetValue("X-Webhook-Secret", out StringValues providedSecretValues))
        {
            _logger.LogWarning("Blocked database webhook request: Missing 'X-Webhook-Secret' header.");
            return Results.Unauthorized();
        }

        string providedSecret = providedSecretValues.ToString();

        byte[] expectedBytes = Encoding.UTF8.GetBytes(_webhookSecret);
        byte[] providedBytes = Encoding.UTF8.GetBytes(providedSecret);

        if (expectedBytes.Length != providedBytes.Length || 
            !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            _logger.LogWarning("Blocked database webhook request: Invalid 'X-Webhook-Secret' value.");
            return Results.Unauthorized();
        }

        return await next(context);
    }
}