using System.Security.Claims;
using System.Text.Json;
using Yarp.ReverseProxy.Transforms;

namespace Kariyer.Identity.Infrastructure.Gateway;

public static class YarpExtensions
{
    public static IServiceCollection AddCustomReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(transformContext =>
                {
                    ClaimsPrincipal user = transformContext.HttpContext.User;

                    transformContext.ProxyRequest.Headers.Remove("X-User-Id");
                    transformContext.ProxyRequest.Headers.Remove("X-User-Email");
                    transformContext.ProxyRequest.Headers.Remove("X-User-Role");

                    if (user.Identity is { IsAuthenticated: true })
                    {
                        string? userId = user.FindFirst("sub")?.Value 
                                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        
                        string? email = user.FindFirst("email")?.Value 
                                     ?? user.FindFirst(ClaimTypes.Email)?.Value;

                        string? role = null;

                        string? userMetadataClaim = user.FindFirst("user_metadata")?.Value;
                        
                        if (!string.IsNullOrEmpty(userMetadataClaim))
                        {
                            try
                            {
                                using JsonDocument document = JsonDocument.Parse(userMetadataClaim);
                                
                                JsonElement accountTypeProp;
                                if (document.RootElement.TryGetProperty("account_type", out accountTypeProp))
                                {
                                    role = accountTypeProp.GetString();
                                }
                            }
                            catch (JsonException)
                            {
                                // Malformed JSON in JWT. 
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(role) && user.HasClaim(c => c.Type == "account_type"))
                        {
                            role = user.FindFirst("account_type")!.Value;
                        }

                        if (string.IsNullOrWhiteSpace(role))
                        {
                            throw new UnauthorizedAccessException($"GATEWAY ERROR: Authenticated user '{userId ?? "UNKNOWN"}' is missing the 'account_type' claim in Supabase. Gateway routing aborted to prevent state corruption.");
                        }

                        if (string.Equals(role, "b", StringComparison.OrdinalIgnoreCase) || 
                            string.Equals(role, "employer", StringComparison.OrdinalIgnoreCase))
                        {
                            role = "company";
                        }

                        if (!string.IsNullOrEmpty(userId))
                        {
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
                        }
                        if (!string.IsNullOrEmpty(email))
                        {
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Email", email);
                        }
                        
                        transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", role);
                    }
                    else
                    {
                        transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", "guest");
                    }

                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }
}