using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;

namespace Kariyer.Identity.Features.AccountLifecycle.GracePeriodSweeper;

public sealed class GracePeriodSweeperWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<GracePeriodSweeperWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public GracePeriodSweeperWorker(
        IServiceProvider serviceProvider,
        IConnectionMultiplexer redis,
        ILogger<GracePeriodSweeperWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_checkInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            StackExchange.Redis.IDatabase db = _redis.GetDatabase();
            RedisKey lockKey = "lock:sweeper:grace_period_deletions";
            RedisValue lockToken = Environment.MachineName;

            bool lockAcquired = await db.LockTakeAsync(lockKey, lockToken, TimeSpan.FromMinutes(10));

            if (!lockAcquired)
            {
                _logger.LogDebug("Another instance is running the grace period sweeper. Skipping.");
                continue;
            }

            try
            {
                await ProcessExpiredGracePeriodsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process expired grace period deletions.");
            }
            finally
            {
                await db.LockReleaseAsync(lockKey, lockToken);
            }
        }
    }

    private async Task ProcessExpiredGracePeriodsAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        IPublishEndpoint publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        bool hasMoreBacklog;
        int batchNumber = 0;

        do
        {
            batchNumber++;
            dbContext.ChangeTracker.Clear();

            DateTimeOffset now = DateTimeOffset.UtcNow;

            List<AccountDeletionSagaState> expiredSagas = await dbContext.Set<AccountDeletionSagaState>()
                .Where(s => s.CurrentState == "GracePeriodActive" && s.GracePeriodEndsAt <= now)
                .OrderBy(s => s.GracePeriodEndsAt)
                .Take(100)
                .ToListAsync(stoppingToken);

            _logger.LogTrace("[DIAG] Grace period sweeper batch {Batch}: found {Count} expired sagas.", batchNumber, expiredSagas.Count);

            if (expiredSagas.Count > 0)
            {
                using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                try
                {
                    foreach (AccountDeletionSagaState saga in expiredSagas)
                    {
                        _logger.LogInformation(
                            "Grace period expired for account {UserUid} (SagaId: {CorrelationId}). Publishing execution command.",
                            saga.UserUid, saga.CorrelationId);

                        await publishEndpoint.Publish(new ExecuteAccountDeletionCommand
                        {
                            UserUid = saga.UserUid
                        }, stoppingToken);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    _logger.LogInformation("Grace period sweeper batch {Batch}: dispatched {Count} deletion executions.", batchNumber, expiredSagas.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Grace period sweeper batch {Batch} failed, rolling back.", batchNumber);
                    await transaction.RollbackAsync(stoppingToken);
                    throw;
                }
            }

            hasMoreBacklog = expiredSagas.Count == 100;

        } while (hasMoreBacklog && !stoppingToken.IsCancellationRequested);
    }
}
