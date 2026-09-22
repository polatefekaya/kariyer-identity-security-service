using Microsoft.AspNetCore.HttpOverrides;

namespace Kariyer.Identity.Infrastructure.Gateway;

/// <summary>
/// Resolves the visitor's real address and scheme from the proxy chain in front of this
/// gateway (Cloudflare → nginx → here), so that everything that reads
/// <c>Connection.RemoteIpAddress</c> — the rate-limit policies in this process, the OTel
/// <c>http.client_ip</c> tag, and every upstream YARP forwards to — sees the person, not
/// the last proxy hop.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS: YARP's default X-Forwarded-For transform is <c>Set</c>, which REPLACES
/// the header with <c>Connection.RemoteIpAddress</c>. Without this middleware that is the
/// address of nginx (loopback or the docker bridge), so every upstream received
/// <c>X-Forwarded-For: 127.0.0.1</c> for every visitor. The Node backend's per-IP limiter
/// on the guest CV import then put the whole site into ONE bucket, and the third upload
/// from anyone locked everyone else out — first-time visitors from an ad were told they
/// had hit their daily limit.
/// </remarks>
public static class ClientAddressExtensions
{
    /// <summary>
    /// Networks whose entries are trusted and stepped over when walking X-Forwarded-For
    /// from right to left; the first address NOT in this list is the visitor. That is what
    /// makes the header spoof-proof: a forged value sent by the client sits to the LEFT of
    /// the address Cloudflare appended for the real connection, and the walk stops before
    /// reaching it.
    ///
    /// Cloudflare's ranges are here because the site is proxied through it: nginx appends
    /// the Cloudflare edge address, and stopping there would key everything on a few
    /// hundred edge IPs instead of on visitors. Published at
    /// https://www.cloudflare.com/ips-v4 and https://www.cloudflare.com/ips-v6 (fetched
    /// 2026-09-22). Override with <c>ForwardedHeaders:KnownNetworks</c> in configuration
    /// if they change — no rebuild needed.
    /// </summary>
    private static readonly string[] DefaultKnownNetworks =
    [
        // This host and the private networks nginx / docker sit on.
        "127.0.0.0/8", "::1/128",
        "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",

        // Cloudflare IPv4
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22",
        "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20",
        "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
        "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",

        // Cloudflare IPv6
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32",
        "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32",
    ];

    public static IServiceCollection AddClientAddressResolution(this IServiceCollection services, IConfiguration configuration)
    {
        string[] knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() is { Length: > 0 } configured
            ? configured
            : DefaultKnownNetworks;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // The default limit is ONE hop, which here would stop at the Cloudflare edge
            // address nginx appended. Walk the whole chain; the known-network check is what
            // bounds it.
            options.ForwardLimit = null;

            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (string cidr in knownNetworks)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
        });

        return services;
    }

    /// <summary>
    /// Must run before anything that reads the connection address or scheme: the request
    /// logger, the rate limiter, and YARP.
    /// </summary>
    public static IApplicationBuilder UseClientAddressResolution(this IApplicationBuilder app)
        => app.UseForwardedHeaders();
}
