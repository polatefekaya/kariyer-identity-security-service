using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Kariyer.Identity.Infrastructure.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Yarp.ReverseProxy.Transforms;

namespace Kariyer.Identity.Infrastructure.Gateway;

public static class YarpExtensions
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private static readonly Dictionary<string, string> ClusterPeerService = new()
    {
        ["supabaseCluster"] = "supabase-auth",
        ["nodeBackendCluster"] = "node-backend",
    };

    public static IServiceCollection AddCustomReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(builderContext =>
            {
                string routeId = builderContext.Route.RouteId;
                string? clusterId = builderContext.Cluster?.ClusterId;
                string peerService = clusterId is not null && ClusterPeerService.TryGetValue(clusterId, out string? ps)
                    ? ps
                    : clusterId ?? "unknown";

                builderContext.AddRequestTransform(transformContext =>
                {
                    ClaimsPrincipal user = transformContext.HttpContext.User;

                    // Strip upstream-injected identity headers to prevent spoofing
                    transformContext.ProxyRequest.Headers.Remove("X-User-Id");
                    transformContext.ProxyRequest.Headers.Remove("X-User-Email");
                    transformContext.ProxyRequest.Headers.Remove("X-User-Role");

                    // Strip the original traceparent/tracestate/baggage that came from the
                    // browser. We re-inject them below using the CURRENT span so the upstream
                    // service is a child of this gateway span — not a sibling of the frontend.
                    transformContext.ProxyRequest.Headers.Remove("traceparent");
                    transformContext.ProxyRequest.Headers.Remove("tracestate");
                    transformContext.ProxyRequest.Headers.Remove("baggage");

                    // Inject the current Activity's context so the upstream spans are properly
                    // linked: frontend → identity-gateway → upstream-service
                    Activity? current = Activity.Current;
                    if (current is not null)
                    {
                        Propagator.Inject(
                            new PropagationContext(current.Context, Baggage.Current),
                            transformContext.ProxyRequest.Headers,
                            static (headers, key, value) => headers.TryAddWithoutValidation(key, value));

                        // Tag the proxy span so SigNoz can identify the upstream
                        current.SetTag("gateway.route", routeId);
                        current.SetTag("gateway.upstream_cluster", clusterId);
                        current.SetTag("peer.service", peerService);
                        current.SetTag("span.kind", "client");

                        if (user.Identity is { IsAuthenticated: true })
                        {
                            string? gatewayUserId = user.FindFirst("sub")?.Value
                                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            string? gatewayRole = user.FindFirst("role")?.Value;
                            if (gatewayUserId is not null) current.SetTag("gateway.user.id", gatewayUserId);
                            if (gatewayRole is not null) current.SetTag("gateway.user.role", gatewayRole);
                        }
                    }

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
                                if (document.RootElement.TryGetProperty("account_type", out JsonElement accountTypeProp))
                                    role = accountTypeProp.GetString();
                            }
                            catch (JsonException) { /* Malformed JSON */ }
                        }

                        if (string.IsNullOrWhiteSpace(role) && user.HasClaim(c => c.Type == "account_type"))
                            role = user.FindFirst("account_type")!.Value;

                        if (string.IsNullOrWhiteSpace(role))
                            throw new UnauthorizedAccessException($"SECURITY ALERT: Authenticated user '{userId ?? "UNKNOWN"}' is missing the 'account_type' claim. Routing aborted to prevent privilege escalation.");

                        role = NormalizeRole(role);

                        if (!string.IsNullOrEmpty(userId))
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
                        if (!string.IsNullOrEmpty(email))
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Email", email);

                        transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", role);
                    }
                    else
                    {
                        transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", "guest");
                    }

                    IdentityDiagnostics.ProxyRequestCounter.Add(1,
                        new KeyValuePair<string, object?>("route", routeId),
                        new KeyValuePair<string, object?>("cluster", clusterId ?? "unknown"),
                        new KeyValuePair<string, object?>("peer_service", peerService),
                        new KeyValuePair<string, object?>("authenticated", user.Identity?.IsAuthenticated == true ? "true" : "false"));

                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }

    private static string NormalizeRole(string rawRole)
    {
        return rawRole.ToLowerInvariant() switch
        {
            "b" or "employer" or "company" => "company",
            "c" or "employee" => "employee",
            "a" or "admin" => "admin",
            "co" or "community" => "community",
            "super_admin" => "super_admin",
            "moderator" => "moderator",
            _ => rawRole.ToLowerInvariant()
        };
    }
}
