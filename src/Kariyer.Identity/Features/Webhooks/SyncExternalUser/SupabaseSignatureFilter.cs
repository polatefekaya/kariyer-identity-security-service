using System.Security.Cryptography;
using System.Text;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public class SupabaseSignatureFilter : IEndpointFilter
{
    private readonly string _webhookSecret;
    private readonly ILogger<SupabaseSignatureFilter> _logger;

    public SupabaseSignatureFilter(IConfiguration config, ILogger<SupabaseSignatureFilter> logger)
    {
        _webhookSecret = config["Supabase:WebhookSecret"] ?? throw new ArgumentNullException("Supabase:WebhookSecret is missing");
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        
        if (!httpContext.Request.Headers.TryGetValue("x-supabase-signature", out var signature))
        {
            _logger.LogWarning("Blocked webhook request: Missing x-supabase-signature header.");
            return Results.Unauthorized();
        }

        httpContext.Request.EnableBuffering();

        using StreamReader reader = new (httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync();
        httpContext.Request.Body.Position = 0;

        using HMACSHA256 hmac = new (Encoding.UTF8.GetBytes(_webhookSecret));
        byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        string calculatedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        if (calculatedSignature != signature.ToString())
        {
            _logger.LogError("Blocked webhook request: Cryptographic signature mismatch.");
            return Results.Unauthorized();
        }

        return await next(context);
    }
}