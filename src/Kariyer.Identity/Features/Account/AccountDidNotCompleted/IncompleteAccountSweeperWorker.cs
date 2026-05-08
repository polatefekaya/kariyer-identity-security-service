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

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[DIAG] ProcessIncompleteAccountsAsync started.");
            _logger.LogTrace("[DIAG] publishEndpoint runtime type: {PublishEndpointType}", publishEndpoint.GetType().FullName);
            _logger.LogTrace("[DIAG] dbContext instance hash: {DbContextHash}", dbContext.GetHashCode());
        }

        bool hasMoreBacklog;
        int batchNumber = 0;

        do
        {
            batchNumber++;
            dbContext.ChangeTracker.Clear();

            _logger.LogTrace("[DIAG] Batch {Batch} starting. ChangeTracker cleared. Tracked entries: {TrackedCount}", batchNumber, dbContext.ChangeTracker.Entries().Count());

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

            _logger.LogTrace("[DIAG] Batch {Batch} query results — Step1 candidates: {Step1Count}, Step2 candidates: {Step2Count}, Total: {Total}",
                batchNumber, companiesToRemindStep1.Count, companiesToRemindStep2.Count, totalProcessedInBatch);

            if (totalProcessedInBatch > 0)
            {
                using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                _logger.LogTrace("[DIAG] Transaction begun for batch {Batch}. TransactionId: {TransactionId}", batchNumber, transaction.TransactionId);

                try
                {
                    foreach (LegacyCompany company in companiesToRemindStep1)
                    {
                        _logger.LogTrace("[DIAG] Publishing AccountDidNotCompletedEvent (Step1/ReminderStep=1) for company Uid: {Uid}", company.Uid);

                        await publishEndpoint.Publish(new AccountDidNotCompletedEvent
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Uid = company.Uid,
                            Email = company.Email,
                            FullName = company.CompanyName ?? company.Username ?? string.Empty,
                            AccountType = "company",
                            ReminderStep = 1
                        }, stoppingToken);

                        _logger.LogTrace("[DIAG] Publish(Step1) returned for Uid: {Uid}. ChangeTracker now has {TrackedCount} entries.", company.Uid, dbContext.ChangeTracker.Entries().Count());

                        company.OnboardingReminderStep = 1;
                    }

                    foreach (LegacyCompany company in companiesToRemindStep2)
                    {
                        _logger.LogTrace("[DIAG] Publishing AccountDidNotCompletedEvent (Step2/ReminderStep=2) for company Uid: {Uid}", company.Uid);

                        await publishEndpoint.Publish(new AccountDidNotCompletedEvent
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Uid = company.Uid,
                            Email = company.Email,
                            FullName = company.CompanyName ?? company.Username ?? string.Empty,
                            AccountType = "company",
                            ReminderStep = 2
                        }, stoppingToken);

                        _logger.LogTrace("[DIAG] Publish(Step2) returned for Uid: {Uid}. ChangeTracker now has {TrackedCount} entries.", company.Uid, dbContext.ChangeTracker.Entries().Count());

                        company.OnboardingReminderStep = 2;
                    }

                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        var entriesBeforeSave = dbContext.ChangeTracker.Entries().ToList();
                        _logger.LogTrace("[DIAG] ChangeTracker state BEFORE SaveChangesAsync() in batch {Batch}. Total tracked: {Count}", batchNumber, entriesBeforeSave.Count);
                        foreach (var entry in entriesBeforeSave)
                            _logger.LogTrace("[DIAG]   -> Entity: {EntityType} | State: {State}", entry.Entity.GetType().FullName, entry.State);

                        bool hasOutboxMessage = entriesBeforeSave.Any(e => e.Entity.GetType().Name.Contains("OutboxMessage"));
                        _logger.LogTrace("[DIAG] OutboxMessage entity present in ChangeTracker before save: {HasOutboxMessage}", hasOutboxMessage);
                    }

                    int savedCount = await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogTrace("[DIAG] SaveChangesAsync() completed for batch {Batch}. Rows affected: {SavedCount}", batchNumber, savedCount);

                    await transaction.CommitAsync(stoppingToken);
                    _logger.LogTrace("[DIAG] transaction.CommitAsync() completed for batch {Batch}. TransactionId: {TransactionId}", batchNumber, transaction.TransactionId);

                    _logger.LogInformation("Swept {Count} incomplete accounts in current batch.", totalProcessedInBatch);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace("[DIAG] Exception in batch {Batch}, rolling back transaction. Exception: {ExceptionType}: {ExceptionMessage}", batchNumber, ex.GetType().Name, ex.Message);
                    await transaction.RollbackAsync(stoppingToken);
                    throw;
                }
            }
            else
            {
                _logger.LogTrace("[DIAG] Batch {Batch} — no accounts to process. Skipping transaction.", batchNumber);
            }

            hasMoreBacklog = companiesToRemindStep1.Count == 500 || companiesToRemindStep2.Count == 500;
            _logger.LogTrace("[DIAG] Batch {Batch} done. hasMoreBacklog: {HasMoreBacklog}", batchNumber, hasMoreBacklog);

        } while (hasMoreBacklog && !stoppingToken.IsCancellationRequested);

        _logger.LogTrace("[DIAG] ProcessIncompleteAccountsAsync completed. Total batches processed: {BatchCount}", batchNumber);
    }
}