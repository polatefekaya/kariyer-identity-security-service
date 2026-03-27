using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public class SupabaseSignatureFilter(IConfiguration config, ILogger<SupabaseSignatureFilter> logger) : IEndpointFilter
{
    private readonly string _webhookSecret = config["ExternalProvider:WebhookSecret"] 
        ?? throw new ArgumentNullException("CRITICAL: ExternalProvider:WebhookSecret is missing from configuration.");
    
    private readonly ILogger<SupabaseSignatureFilter> _logger = logger;
    private const int ToleranceSeconds = 300;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue("webhook-id", out StringValues msgId) ||
            !httpContext.Request.Headers.TryGetValue("webhook-timestamp", out StringValues msgTimestamp) ||
            !httpContext.Request.Headers.TryGetValue("webhook-signature", out StringValues signatureHeader))
        {
            _logger.LogWarning("Blocked webhook request: Missing Standard Webhook headers.");
            return Results.Unauthorized();
        }

        if (!long.TryParse(msgTimestamp, out long timestampUnix))
        {
            _logger.LogWarning("Blocked webhook request: Invalid timestamp format.");
            return Results.Unauthorized();
        }

        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        if (Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalSeconds) > ToleranceSeconds)
        {
            _logger.LogWarning("Blocked webhook request: Timestamp outside tolerance zone (Possible Replay Attack).");
            return Results.Unauthorized();
        }

        httpContext.Request.EnableBuffering();
        using StreamReader reader = new(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync();
        httpContext.Request.Body.Position = 0;

        string secretPrefixRemoved = _webhookSecret.StartsWith("whsec_") ? _webhookSecret[6..] : _webhookSecret;
        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(secretPrefixRemoved);
        }
        catch (FormatException)
        {
            _logger.LogError("CRITICAL: Webhook secret is not valid Base64.");
            return Results.StatusCode(500);
        }

        string signedContent = $"{msgId}.{msgTimestamp}.{rawBody}";
        byte[] signedContentBytes = Encoding.UTF8.GetBytes(signedContent);

        using HMACSHA256 hmac = new(secretBytes);
        byte[] expectedHash = hmac.ComputeHash(signedContentBytes);

        string[] passedSignatures = signatureHeader.ToString().Split(' ');
        bool isValidSignature = false;

        foreach (string versionedSignature in passedSignatures)
        {
            string[] parts = versionedSignature.Split(',');
            if (parts.Length < 2) continue;

            string version = parts[0];
            string base64Signature = parts[1];

            if (version == "v1")
            {
                byte[] providedSignatureBytes;
                try
                {
                    providedSignatureBytes = Convert.FromBase64String(base64Signature);
                }
                catch (FormatException)
                {
                    continue;
                }

                if (CryptographicOperations.FixedTimeEquals(expectedHash, providedSignatureBytes))
                {
                    isValidSignature = true;
                    break;
                }
            }
        }

        if (!isValidSignature)
        {
            _logger.LogError("Blocked webhook request: Cryptographic signature mismatch.");
            return Results.Unauthorized();
        }

        return await next(context);
    }
}