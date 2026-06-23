using Microsoft.AspNetCore.Authorization;

namespace Kariyer.Identity.Infrastructure.Auth;

public class AdminDbRequirement : IAuthorizationRequirement;

public class SuperAdminDbRequirement : IAuthorizationRequirement;
