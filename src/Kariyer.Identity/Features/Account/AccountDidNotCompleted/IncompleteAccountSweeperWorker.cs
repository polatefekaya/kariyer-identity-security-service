using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Domain.Entities;

namespace Kariyer.Identity.Features.Account.AccountDidNotCompleted;

public sealed class IncompleteAccountSweeperWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<IncompleteAccountSweeperWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public IncompleteAccountSweeperWorker(
        IServiceProvider serviceProvider,
        IConnectionMultiplexer redis,
        ILogger<IncompleteAccountSweeperWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new (_checkInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            IDatabase db = _redis.GetDatabase();
            RedisKey lockKey = "lock:sweeper:incomplete_accounts";
            RedisValue lockToken = Environment.MachineName;

            bool lockAcquired = await db.LockTakeAsync(lockKey, lockToken, TimeSpan.FromMinutes(10));

            if (!lockAcquired)
            {
                _logger.LogDebug("Another instance is running the sweeper. Skipping.");
                continue;
            }

            try
            {
                await ProcessIncompleteAccountsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process incomplete accounts.");
            }
            finally
            {
                await db.LockReleaseAsync(lockKey, lockToken);
            }
        }
    }

    private async Task ProcessIncompleteAccountsAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        IPublishEndpoint publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        DateTime now = DateTime.UtcNow;
        DateTime day1Threshold = now.AddDays(-1);
        DateTime day3Threshold = now.AddDays(-3);

        List<LegacyCompany> companiesToRemindStep1 = await dbContext.Companies
            .Where(c => !c.IsAccountCompleted 
                     && c.OnboardingReminderStep == 0 
                     && c.CreatedDate <= day1Threshold)
            .Take(500)
            .ToListAsync(stoppingToken);

        foreach (LegacyCompany company in companiesToRemindStep1)
        {
            AccountDidNotCompletedEvent reminderEvent = new()
            {
                MessageId = Guid.NewGuid().ToString(),
                Uid = company.Uid,
                Email = company.Email,
                FullName = company.CompanyName ?? company.Username,
                AccountType = "company",
                ReminderStep = 1
            };

            await publishEndpoint.Publish(reminderEvent, stoppingToken);

            company.OnboardingReminderStep = 1;
        }

        // Fetch companies that need Day 3 reminder
        List<LegacyCompany> companiesToRemindStep2 = await dbContext.Companies
            .Where(c => !c.IsAccountCompleted 
                     && c.OnboardingReminderStep == 1 
                     && c.CreatedDate <= day3Threshold)
            .Take(500)
            .ToListAsync(stoppingToken);

        foreach (LegacyCompany company in companiesToRemindStep2)
        {
            AccountDidNotCompletedEvent reminderEvent = new()
            {
                MessageId = Guid.NewGuid().ToString(),
                Uid = company.Uid,
                Email = company.Email,
                FullName = company.CompanyName ?? company.Username,
                AccountType = "company",
                ReminderStep = 2
            };

            await publishEndpoint.Publish(reminderEvent, stoppingToken);

            company.OnboardingReminderStep = 2;
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Swept {Count} incomplete accounts.", companiesToRemindStep1.Count + companiesToRemindStep2.Count);
    }
}