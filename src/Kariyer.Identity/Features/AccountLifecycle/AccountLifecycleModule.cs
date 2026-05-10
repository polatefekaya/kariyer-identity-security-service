using Kariyer.Identity.Features.AccountLifecycle.CancelDeletion;
using Kariyer.Identity.Features.AccountLifecycle.FreezeAccount;
using Kariyer.Identity.Features.AccountLifecycle.GetDeletionStatus;
using Kariyer.Identity.Features.AccountLifecycle.RequestDeletion;
using Kariyer.Identity.Features.AccountLifecycle.RestoreAccount;

namespace Kariyer.Identity.Features.AccountLifecycle;

public static class AccountLifecycleModule
{
    public static IServiceCollection AddAccountLifecycleFeature(this IServiceCollection services)
    {
        services.AddScoped<IFreezeAccountService, FreezeAccountService>();
        services.AddScoped<IRestoreAccountService, RestoreAccountService>();
        services.AddScoped<IRequestDeletionService, RequestDeletionService>();
        services.AddScoped<ICancelDeletionService, CancelDeletionService>();
        services.AddScoped<IGetDeletionStatusService, GetDeletionStatusService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAccountLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/accounts")
            .RequireAuthorization()
            .RequireRateLimiting("AccountLifecycle")
            .WithTags("AccountLifecycle");

        group.MapFreezeAccount();
        group.MapRestoreAccount();
        group.MapRequestDeletion();
        group.MapCancelDeletion();     // internally adds RequireAuthorization("RequireAdmin")
        group.MapGetDeletionStatus();

        return app;
    }
}
