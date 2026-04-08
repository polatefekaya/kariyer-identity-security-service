using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kariyer.Identity.Infrastructure.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddSupabaseJwtAuthentication(this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        string externalProviderUrl = configuration["ExternalProvider:Url"]
                ?? throw new ArgumentNullException("ExternalProvider:Url missing");
                
        string externalProviderJwt = configuration["ExternalProvider:JwtSecret"]
                ?? throw new ArgumentNullException("ExternalProvider:JwtSecret missing");
    
        byte[] signingKeyBytes = Encoding.UTF8.GetBytes(externalProviderJwt);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = $"{externalProviderUrl}/auth/v1",
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = "role"
                };
    
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        logger.LogWarning("JWT Validation Failed: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity)
                        {
                            Claim? userMetaDataClaim = identity.FindFirst("user_metadata");

                            if (userMetaDataClaim != null)
                            {
                                using JsonDocument document = JsonDocument.Parse(userMetaDataClaim.Value);
                                JsonElement root = document.RootElement;

                                if (root.TryGetProperty("account_type", out JsonElement accountTypeElement))
                                {
                                    string? role = accountTypeElement.GetString();
                                    if (!string.IsNullOrWhiteSpace(role))
                                    {
                                        identity.AddClaim(new Claim("role", role));
                                    }
                                }
                            }
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdmin", policy => 
                policy.RequireRole("admin", "super_admin"))
            .AddPolicy("RequireSuperAdmin", policy => 
                policy.RequireRole("super_admin"));

        return services;
    }
}