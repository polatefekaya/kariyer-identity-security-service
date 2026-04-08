using System.Diagnostics;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Shared;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;

namespace Kariyer.Identity.Features.Admins.CreateAdmin;

internal sealed class CreateAdminService(
    IdentityDbContext dbContext,
    ISupabaseAdminAuthService supabaseAuth,
    ILogger<CreateAdminService> logger) : ICreateAdminService
{
    public async Task<ApiResponse<CreateAdminResponseData>> HandleAsync(CreateAdminRequest request, CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityDiagnostics.ActivitySource.StartActivity("CreateAdmin");
        activity?.SetTag("admin.email", request.Email);
        activity?.SetTag("admin.role", request.Role);

        try
        {
            logger.LogInformation("Attempting to create admin in Supabase Auth for {Email}", request.Email);
            
            Guid externalId = await supabaseAuth.CreateUserAsync(
                            email: request.Email,
                            password: request.Password,
                            firstName: request.Name,
                            lastName: request.Surname,
                            accountType: "admin", 
                            cancellationToken: cancellationToken);

            activity?.AddEvent(new ActivityEvent("SupabaseUserCreated"));

            LegacyAdmin newAdmin = LegacyAdmin.CreateFromExternalProvider(
                externalId: externalId,
                email: request.Email,
                phone: string.Empty,
                name: request.Name,
                surname: request.Surname,
                role: request.Role
            );

            newAdmin.CompleteAccount();

            await dbContext.Admins.AddAsync(newAdmin, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            IdentityDiagnostics.AdminOperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "create"));

            return new ApiResponse<CreateAdminResponseData>(
                Success: true,
                Message: "Yönetici başarıyla oluşturuldu.",
                Data: new CreateAdminResponseData(newAdmin.Uid)
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to create admin {Email}", request.Email);
            throw;
        }
    }
}