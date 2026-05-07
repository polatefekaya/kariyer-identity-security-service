using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Messaging.Contracts.Account;
using Microsoft.EntityFrameworkCore.Storage;

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
            StackExchange.Redis.IDatabase db = _redis.GetDatabase();
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

        bool hasMoreBacklog;

        do
        {
            dbContext.ChangeTracker.Clear();

            DateTime now = DateTime.UtcNow;
            DateTime day1Threshold = now.AddDays(-1);
            DateTime day3Threshold = now.AddDays(-3);

            List<LegacyCompany> companiesToRemindStep1 = await dbContext.Companies
                .Where(c => !c.IsAccountCompleted
                         && c.OnboardingReminderStep == 0
                         && c.CreatedDate <= day1Threshold)
                .OrderBy(c => c.CreatedDate)
                .Take(500)
                .ToListAsync(stoppingToken);

            List<LegacyCompany> companiesToRemindStep2 = await dbContext.Companies
                .Where(c => !c.IsAccountCompleted
                         && c.OnboardingReminderStep == 1
                         && c.CreatedDate <= day3Threshold)
                .OrderBy(c => c.CreatedDate)
                .Take(500)
                .ToListAsync(stoppingToken);

            int totalProcessedInBatch = companiesToRemindStep1.Count + companiesToRemindStep2.Count;

            if (totalProcessedInBatch > 0)
            {
                using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                try
                {
                    foreach (LegacyCompany company in companiesToRemindStep1)
                    {
                        await publishEndpoint.Publish(new AccountDidNotCompletedEvent
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Uid = company.Uid,
                            Email = company.Email,
                            FullName = company.CompanyName ?? company.Username,
                            AccountType = "company",
                            ReminderStep = 1
                        }, stoppingToken);
                        company.OnboardingReminderStep = 1;
                    }

                    foreach (LegacyCompany company in companiesToRemindStep2)
                    {
                        await publishEndpoint.Publish(new AccountDidNotCompletedEvent
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Uid = company.Uid,
                            Email = company.Email,
                            FullName = company.CompanyName ?? company.Username,
                            AccountType = "company",
                            ReminderStep = 2
                        }, stoppingToken);
                        company.OnboardingReminderStep = 2;
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    _logger.LogInformation("Swept {Count} incomplete accounts in current batch.", totalProcessedInBatch);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(stoppingToken);
                    throw;
                }
            }

            hasMoreBacklog = companiesToRemindStep1.Count == 500 || companiesToRemindStep2.Count == 500;

        } while (hasMoreBacklog && !stoppingToken.IsCancellationRequested);
    }
}