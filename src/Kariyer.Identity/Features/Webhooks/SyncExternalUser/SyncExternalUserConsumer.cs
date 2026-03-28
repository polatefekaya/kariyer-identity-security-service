using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public class SyncExternalUserConsumer : IConsumer<ExternalUserCreatedEvent>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ILogger<SyncExternalUserConsumer> _logger;

    public SyncExternalUserConsumer(IdentityDbContext dbContext, ILogger<SyncExternalUserConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExternalUserCreatedEvent> context)
    {
        _logger.LogInformation("Processing Supabase UUID: {UserId}", context.Message.UserId);
        ExternalUserCreatedEvent message = context.Message;
        bool isCompany = message.AccountType == "b";

        if (isCompany)
        {
            LegacyCompany? existingCompany = await _dbContext.Companies
                .FirstOrDefaultAsync(c => c.Email == message.Email, context.CancellationToken);

            if (existingCompany != null)
            {
                if (existingCompany.ExternalId == message.UserId) return; 

                existingCompany.LinkExternalAccount(message.UserId);
                _dbContext.Companies.Update(existingCompany);
                _logger.LogInformation("Migrated Legacy Company: {Email} to External ID: {UserId}", message.Email, message.UserId);
            }
            else
            {
                LegacyCompany newCompany = LegacyCompany.CreateFromExternalProvider(
                    message.UserId, message.Email, message.PhoneNumber, message.FirstName, message.LastName
                );
                await _dbContext.Companies.AddAsync(newCompany, context.CancellationToken);
                _logger.LogInformation("Created New Company: {Email}. Pending frontend onboarding.", message.Email);
            }
        }
        else
        {
            LegacyEmployee? existingEmployee = await _dbContext.Employees
                .FirstOrDefaultAsync(e => e.Email == message.Email, context.CancellationToken);

            if (existingEmployee != null)
            {
                if (existingEmployee.ExternalId == message.UserId) return; 

                existingEmployee.LinkExternalAccount(message.UserId);
                _dbContext.Employees.Update(existingEmployee);
                _logger.LogInformation("Migrated Legacy Employee: {Email} to External ID: {UserId}", message.Email, message.UserId);
            }
            else
            {
                LegacyEmployee newEmployee = LegacyEmployee.CreateFromExternalProvider(
                    message.UserId, message.Email, message.PhoneNumber, message.FirstName, message.LastName
                );
                await _dbContext.Employees.AddAsync(newEmployee, context.CancellationToken);
                _logger.LogInformation("Created New Employee: {Email}. Pending frontend onboarding.", message.Email);
            }
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}