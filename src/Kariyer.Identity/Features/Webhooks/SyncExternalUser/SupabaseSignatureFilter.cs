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
        var request = context.HttpContext.Request;

        request.EnableBuffering();
        using StreamReader reader = new(request.Body, Encoding.UTF8, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        string cleanSecret = _webhookSecret.StartsWith("whsec_") ? _webhookSecret[6..] : _webhookSecret;

        if (request.Headers.TryGetValue("webhook-signature", out StringValues svixSignature))
        {
            if (!VerifySvixSignature(request.Headers, rawBody, cleanSecret, svixSignature))
            {
                _logger.LogWarning("Blocked webhook request: Svix cryptographic signature mismatch.");
                return Results.Unauthorized();
            }
            return await next(context);
        }

        if (request.Headers.TryGetValue("x-supabase-signature", out StringValues legacySignature))
        {
            if (!VerifyLegacySignature(rawBody, cleanSecret, legacySignature))
            {
                _logger.LogWarning("Blocked webhook request: Legacy cryptographic signature mismatch.");
                return Results.Unauthorized();
            }
            return await next(context);
        }

        _logger.LogWarning("Blocked webhook request: Missing ALL Supabase signature headers.");
        return Results.Unauthorized();
    }

    private bool VerifySvixSignature(IHeaderDictionary headers, string rawBody, string secret, string signatureHeader)
    {
        if (!headers.TryGetValue("webhook-id", out var msgId) || !headers.TryGetValue("webhook-timestamp", out var msgTimestamp))
            return false;

        if (!long.TryParse(msgTimestamp, out long timestampUnix))
            return false;

        if (Math.Abs((DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestampUnix)).TotalSeconds) > ToleranceSeconds)
            return false; // Replay attack

        try
        {
            byte[] secretBytes = Convert.FromBase64String(secret);
            string signedContent = $"{msgId}.{msgTimestamp}.{rawBody}";
            using HMACSHA256 hmac = new(secretBytes);
            byte[] expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));

            string[] passedSignatures = signatureHeader.ToString().Split(' ');
            foreach (string versionedSignature in passedSignatures)
            {
                string[] parts = versionedSignature.Split(',');
                if (parts.Length < 2) continue;

                if (parts[0] == "v1" && CryptographicOperations.FixedTimeEquals(expectedHash, Convert.FromBase64String(parts[1])))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private bool VerifyLegacySignature(string rawBody, string secret, string signatureHeader)
    {
        try
        {
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
            byte[] expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            
            string expectedSignatureHex = BitConverter.ToString(expectedHash).Replace("-", "").ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignatureHex), 
                Encoding.UTF8.GetBytes(signatureHeader.ToString()));
        }
        catch { }
        return false;
    }
}