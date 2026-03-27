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
        HttpRequest request = context.HttpContext.Request;

        request.EnableBuffering();
        request.Body.Position = 0;

        using StreamReader reader = new(request.Body, Encoding.UTF8, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        string cleanSecret = _webhookSecret;
        if (cleanSecret.StartsWith("v1,"))
        {
            cleanSecret = cleanSecret[3..];
        }
        if (cleanSecret.StartsWith("whsec_"))
        {
            cleanSecret = cleanSecret[6..];
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(cleanSecret);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Webhook secret is not valid Base64 after stripping. Attempting UTF-8 fallback.");
            secretBytes = Encoding.UTF8.GetBytes(cleanSecret);
        }

        if (request.Headers.TryGetValue("webhook-signature", out StringValues svixSignature))
        {
            if (!VerifySvixSignature(request.Headers, rawBody, secretBytes, svixSignature.ToString()))
            {
                _logger.LogWarning("Blocked webhook request: Svix cryptographic signature mismatch.");
                return Results.Unauthorized();
            }
            return await next(context);
        }

        if (request.Headers.TryGetValue("x-supabase-signature", out StringValues legacySignature))
        {
            if (!VerifyLegacySignature(rawBody, secretBytes, legacySignature.ToString()))
            {
                _logger.LogWarning("Blocked webhook request: Legacy cryptographic signature mismatch.");
                return Results.Unauthorized();
            }
            return await next(context);
        }

        _logger.LogWarning("Blocked webhook request: Missing ALL Supabase signature headers.");
        return Results.Unauthorized();
    }

    private bool VerifySvixSignature(IHeaderDictionary headers, string rawBody, byte[] secretBytes, string signatureHeader)
    {
        if (!headers.TryGetValue("webhook-id", out StringValues msgId) || !headers.TryGetValue("webhook-timestamp", out StringValues msgTimestamp))
        {
            return false;
        }

        if (!long.TryParse(msgTimestamp, out long timestampUnix))
        {
            return false;
        }

        if (Math.Abs((DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestampUnix)).TotalSeconds) > ToleranceSeconds)
        {
            return false;
        }

        try
        {
            string signedContent = $"{msgId}.{msgTimestamp}.{rawBody}";
            using HMACSHA256 hmac = new(secretBytes);
            byte[] expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
            
            string[] passedSignatures = signatureHeader.Split(' ');
            foreach (string versionedSignature in passedSignatures)
            {
                string[] parts = versionedSignature.Split(',');
                if (parts.Length < 2) continue;

                if (parts[0] == "v1")
                {
                    try
                    {
                        byte[] providedSignatureBytes = Convert.FromBase64String(parts[1]);
                        if (CryptographicOperations.FixedTimeEquals(expectedHash, providedSignatureBytes))
                        {
                            return true;
                        }
                    }
                    catch (FormatException) { }
                }
            }
        }
        catch { }

        return false;
    }

    private bool VerifyLegacySignature(string rawBody, byte[] secretBytes, string signatureHeader)
    {
        try
        {
            using HMACSHA256 hmac = new(secretBytes);
            byte[] expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));

            string expectedSignatureHex = BitConverter.ToString(expectedHash).Replace("-", "").ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignatureHex),
                Encoding.UTF8.GetBytes(signatureHeader));
        }
        catch { }

        return false;
    }
}
