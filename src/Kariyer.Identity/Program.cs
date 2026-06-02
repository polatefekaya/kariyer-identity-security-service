using System.Reflection;
using System.Threading.RateLimiting;
using MassTransit;
using MassTransit.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using StackExchange.Redis;
using Kariyer.Identity.Domain.Entities;
using Kariyer.Identity.Features.Account.AccountDidNotCompleted;
using Kariyer.Identity.Features.AccountCredentials;
using Kariyer.Identity.Features.AccountCredentials.Saga;
using Kariyer.Identity.Features.AccountLifecycle;
using Kariyer.Identity.Features.AccountLifecycle.GracePeriodSweeper;
using Kariyer.Identity.Features.AccountLifecycle.Saga;
using Kariyer.Identity.Features.Admins;
using Kariyer.Identity.Features.Webhooks.SyncExternalUser;
using Kariyer.Identity.Infrastructure.Auth;
using Kariyer.Identity.Infrastructure.Gateway;
using Kariyer.Identity.Infrastructure.Persistence;
using Kariyer.Identity.Infrastructure.Telemetry;
using Kariyer.Messaging.Contracts.Account;
using MassTransit.Monitoring;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    bool isEfDesignMode = args.Any(a => a.Contains("ef", StringComparison.OrdinalIgnoreCase));

    string garnetConn = builder.Configuration.GetConnectionString("Garnet") ?? string.Empty;
    string dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    string rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    string externalProviderUrl = builder.Configuration["ExternalProvider:Url"] ?? string.Empty;
    string externalProviderJwt = builder.Configuration["ExternalProvider:JwtSecret"] ?? string.Empty;
    string otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? string.Empty;
    string serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    // Signal-specific endpoint overrides the base; mirrors how the OTel SDK resolves env vars.
    string otlpLogsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")
        ?? (string.IsNullOrWhiteSpace(otlpEndpoint) ? string.Empty : otlpEndpoint.TrimEnd('/') + "/v1/logs");

    // OTEL_EXPORTER_OTLP_HEADERS format: "key=value,key2=value2"
    // The OTel SDK reads this automatically for traces/metrics but the Serilog sink does not.
    Dictionary<string, string> otlpHeaders = (Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS") ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(h => h.Split('=', 2))
        .Where(p => p.Length == 2)
        .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

    if (!isEfDesignMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(garnetConn, nameof(garnetConn));
        ArgumentException.ThrowIfNullOrWhiteSpace(dbConnectionString, nameof(dbConnectionString));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProviderUrl, nameof(externalProviderUrl));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProviderJwt, nameof(externalProviderJwt));
    }

    builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
    {
        ConfigurationOptions options = ConfigurationOptions.Parse(garnetConn);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("StrictFrontendPolicy", policy =>
        {
            policy.WithOrigins(
                "https://www.kariyerzamani.com",
                "https://auth.kariyerzamani.com",
                "https://kz-auth.kariyerzamani.com",
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:5173",
                "https://kariyerzamani.com",
                "https://kz-admin.kariyerzamani.com",
                "https://admin.kariyerzamani.com",
                "https://tst.kariyerzamani.com"
            )
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .AllowAnyHeader()
            .AllowCredentials();
        });
    });

    builder.Services.AddSerilog((services, lc) =>
    {
        // ReadFrom.Configuration applies MinimumLevel and Enrich from appsettings.
        // Global minimum is Verbose — sinks apply their own per-level restrictions below.
        lc.ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.With<TraceContextEnricher>()
            .Enrich.WithProperty("deployment.environment", builder.Environment.EnvironmentName)
            .Enrich.WithProperty("service.name", IdentityDiagnostics.ServiceName)
            .Enrich.WithProperty("service.version", serviceVersion)
            .Enrich.WithProperty("host.name", Environment.MachineName)
            // Console: human-readable, restricted to Information+ to avoid noise
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj} {TraceId}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information);

        if (!string.IsNullOrWhiteSpace(otlpLogsEndpoint))
        {
            // OTLP: all levels flow here (Verbose through Fatal).
            // IncludedData ensures trace_id / span_id are set as proper OTel log record
            // fields (not just string attributes), enabling log-trace correlation in SigNoz.
            lc.WriteTo.OpenTelemetry(
                opts =>
                {
                    opts.Endpoint = otlpLogsEndpoint;
                    opts.Protocol = OtlpProtocol.HttpProtobuf;
                    opts.Headers = otlpHeaders;
                    opts.IncludedData =
                        IncludedData.TraceIdField |
                        IncludedData.SpanIdField |
                        IncludedData.MessageTemplateRenderingsAttribute |
                        IncludedData.SpecRequiredFields;
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = IdentityDiagnostics.ServiceName,
                        ["service.version"] = serviceVersion,
                        ["deployment.environment"] = builder.Environment.EnvironmentName,
                        ["host.name"] = Environment.MachineName,
                    };
                },
                restrictedToMinimumLevel: LogEventLevel.Verbose);
        }
        else
        {
            Log.Warning("OTEL_EXPORTER_OTLP_ENDPOINT is not set — logs will not be exported to SigNoz.");
        }
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: IdentityDiagnostics.ServiceName,
                serviceVersion: serviceVersion,
                autoGenerateServiceInstanceId: true)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName,
                ["host.name"] = Environment.MachineName,
            }))
        .WithTracing(tracing => tracing
            .AddSource(IdentityDiagnostics.ServiceName)
            .AddSource(DiagnosticHeaders.DefaultListenerName)
            .AddAspNetCoreInstrumentation(opts =>
            {
                // Exclude health-check noise from traces
                opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                // Record exceptions as span events (full stack trace in SigNoz)
                opts.RecordException = true;
                opts.EnrichWithHttpRequest = (activity, request) =>
                {
                    if (request.HttpContext.Connection.RemoteIpAddress is { } ip)
                        activity.SetTag("http.client_ip", ip.ToString());
                    string? ua = request.Headers.UserAgent.ToString();
                    if (!string.IsNullOrEmpty(ua))
                        activity.SetTag("http.user_agent", ua);
                };
            })
            .AddHttpClientInstrumentation(opts =>
            {
                opts.RecordException = true;
            })
            .AddNpgsql()
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddMeter(IdentityDiagnostics.ServiceName)
            .AddMeter(InstrumentationOptions.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());

    builder.Services.AddDbContext<IdentityDbContext>(options =>
    {
        if (string.IsNullOrWhiteSpace(dbConnectionString)) return; 

        options.UseNpgsql(dbConnectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3, 
                maxRetryDelay: TimeSpan.FromSeconds(2), 
                errorCodesToAdd: null);
        });
    });

    builder.Services.AddMassTransit(busConfigurator =>
    {
        busConfigurator.SetKebabCaseEndpointNameFormatter();
        
        busConfigurator.AddEntityFrameworkOutbox<IdentityDbContext>(outboxConfigurator =>
        {
            outboxConfigurator.UsePostgres();
            outboxConfigurator.UseBusOutbox();
            outboxConfigurator.QueryDelay = TimeSpan.FromSeconds(1);
        });

        busConfigurator.AddSagaStateMachine<AccountDeletionSaga, AccountDeletionSagaState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                r.ExistingDbContext<IdentityDbContext>();
                r.UsePostgres();
            });

        busConfigurator.AddSagaStateMachine<CredentialUpdateSaga, CredentialUpdateSagaState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                r.ExistingDbContext<IdentityDbContext>();
                r.UsePostgres();
            });

        busConfigurator.AddConsumer<BanUserForDeletionConsumer>();
        busConfigurator.AddConsumer<UnbanUserConsumer>();
        busConfigurator.AddConsumer<DeleteUserPermanentlyConsumer>();
        busConfigurator.AddConsumer<Kariyer.Identity.Features.AccountCredentials.Saga.UpdateCredentialInSupabaseConsumer>();
        busConfigurator.AddConsumer<Kariyer.Identity.Features.AccountCredentials.Saga.RevertCredentialInDbConsumer>();

        busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
        {
            rabbitConfigurator.Host(rabbitHost, "/", hostConfigurator =>
            {
                hostConfigurator.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                hostConfigurator.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            rabbitConfigurator.UseMessageRetry(retryConfigurator =>
                retryConfigurator.Interval(3, TimeSpan.FromSeconds(5)));

            rabbitConfigurator.Message<AccountCreatedEvent>(topology => topology.SetEntityName("identity.account.created"));
            rabbitConfigurator.Message<AccountDidNotCompletedEvent>(topology => topology.SetEntityName("identity.account.not-completed"));
            rabbitConfigurator.Message<AccountFrozenEvent>(topology => topology.SetEntityName("identity.account.frozen"));
            rabbitConfigurator.Message<AccountDeletedEvent>(topology => topology.SetEntityName("identity.account.deleted"));
            rabbitConfigurator.Message<AccountDeletionCancelledEvent>(topology => topology.SetEntityName("identity.account.deletion-cancelled"));
            rabbitConfigurator.Message<AccountEmailChangedEvent>(topology => topology.SetEntityName("identity.account.email-changed"));
            rabbitConfigurator.Message<AccountPhoneChangedEvent>(topology => topology.SetEntityName("identity.account.phone-changed"));
            rabbitConfigurator.Message<AccountUsernameChangedEvent>(topology => topology.SetEntityName("identity.account.username-changed"));

            rabbitConfigurator.ConfigureEndpoints(context);
        });
    });

    Supabase.SupabaseOptions supabaseOptions = new()
    {
        AutoRefreshToken = false, 
        AutoConnectRealtime = false
    };
    
    builder.Services.AddSingleton<Supabase.Client>(provider => 
        new Supabase.Client(externalProviderUrl, externalProviderJwt, supabaseOptions));

    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("AccountLifecycle", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.FindFirst("sub")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("AdminOperations", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.FindFirst("sub")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = (ctx, _) =>
        {
            string policy = ctx.HttpContext.GetEndpoint()?.Metadata
                .GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName
                ?? "unknown";
            IdentityDiagnostics.RateLimitRejectedCounter.Add(1,
                new KeyValuePair<string, object?>("policy", policy));
            return ValueTask.CompletedTask;
        };
    });

    builder.Services.AddSupabaseJwtAuthentication(builder.Configuration, Log.Logger);
    builder.Services.AddAdminFeature();
    builder.Services.AddAccountLifecycleFeature();
    builder.Services.AddAccountCredentialsFeature();
    builder.Services.AddScoped<ISupabaseAdminAuthService, SupabaseAdminAuthService>();
    builder.Services.AddHostedService<IncompleteAccountSweeperWorker>();
    builder.Services.AddHostedService<GracePeriodSweeperWorker>();
    builder.Services.AddCustomReverseProxy(builder.Configuration);

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebhookJsonContext.Default);
    });

    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionString: dbConnectionString, 
            name: "PostgreSQL", 
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "db"])
        .AddRedis(
            redisConnectionString: garnetConn, 
            name: "Garnet", 
            failureStatus: HealthStatus.Degraded,
            tags: ["ready", "cache"]);

    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    });

    WebApplication app = builder.Build();

    if (!isEfDesignMode)
    {
        using IServiceScope scope = app.Services.CreateScope();
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogInformation("Attempting to apply Entity Framework migrations...");
            IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            dbContext.Database.Migrate();
            logger.LogInformation("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CRITICAL: Database migration failed. The application will crash.");
            throw;
        }
    }
    
    if (!isEfDesignMode)
    {
        Supabase.Client supabaseClient = app.Services.GetRequiredService<Supabase.Client>();
        await supabaseClient.InitializeAsync();
    }

    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                new { success = false, message = "Sunucu hatası oluştu. Lütfen tekrar deneyin.", data = (object?)null });
        });
    });

    app.Use(async (HttpContext context, Func<Task> next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/webhooks/supabase", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.ContentType = "application/json";
        }
        context.Request.EnableBuffering();
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseCors("StrictFrontendPolicy");
    app.UseMiddleware<BaggageEnricherMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready") || true 
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapReverseProxy();
    app.MapSyncSupabaseUserEndpoint();
    app.MapAdminEndpoints();
    app.MapAccountLifecycleEndpoints();
    app.MapAccountCredentialsEndpoints();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Identity Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}