using Kariyer.Identity.Features.AccountCredentials.UpdateEmail;
using Kariyer.Identity.Features.AccountCredentials.UpdatePhone;
using Kariyer.Identity.Features.AccountCredentials.UpdateUsername;

namespace Kariyer.Identity.Features.AccountCredentials;

public static class AccountCredentialsModule
{
    public static IServiceCollection AddAccountCredentialsFeature(this IServiceCollection services)
    {
        services.AddScoped<IUpdateEmailService, UpdateEmailService>();
        services.AddScoped<IUpdatePhoneService, UpdatePhoneService>();
        services.AddScoped<IUpdateUsernameService, UpdateUsernameService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAccountCredentialsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/accounts")
            .RequireAuthorization()
            .RequireRateLimiting("AccountLifecycle")
            .WithTags("AccountCredentials");

        group.MapUpdateEmail();
        group.MapUpdatePhone();
        group.MapUpdateUsername();

        return app;
    }
}
