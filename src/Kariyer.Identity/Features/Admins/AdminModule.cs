using Kariyer.Identity.Features.Admins.CreateAdmin;
using Kariyer.Identity.Features.Admins.DeleteAdmin;
using Kariyer.Identity.Features.Admins.GetAdmin;
using Kariyer.Identity.Features.Admins.GetAdmins;
using Kariyer.Identity.Features.Admins.UpdateAdminRole;
using Kariyer.Identity.Features.Admins.UpdateAdminStatus;

namespace Kariyer.Identity.Features.Admins;

public static class AdminModule
{
    public static IServiceCollection AddAdminFeature(this IServiceCollection services)
    {
        services.AddScoped<ICreateAdminService, CreateAdminService>();
        services.AddScoped<IGetAdminsService, GetAdminsService>();
        services.AddScoped<IGetAdminService, GetAdminService>();
        services.AddScoped<IUpdateAdminRoleService, UpdateAdminRoleService>();
        services.AddScoped<IUpdateAdminStatusService, UpdateAdminStatusService>();
        services.AddScoped<IDeleteAdminService, DeleteAdminService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder adminGroup = app.MapGroup("/api/admins")
            .RequireAuthorization("RequireAdmin");

        adminGroup.MapCreateAdmin();
        adminGroup.MapGetAdmins();
        adminGroup.MapGetAdmin();

        adminGroup.MapUpdateAdminRole();
        //.RequireAuthorization("RequireSuperAdmin");
        adminGroup.MapDeleteAdmin();
        //.RequireAuthorization("RequireSuperAdmin");

        adminGroup.MapUpdateAdminStatus();

        return app;
    }
}
