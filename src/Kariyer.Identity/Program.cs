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
using StackExchange.Redis;
using Kariyer.Identity.Features.Account.AccountDidNotCompleted;
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
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        });
    });

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(IdentityDiagnostics.ServiceName))
        .WithTracing(tracing => tracing
            .AddSource(IdentityDiagnostics.ServiceName)
            .AddSource(DiagnosticHeaders.DefaultListenerName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddMeter(IdentityDiagnostics.ServiceName)
            .AddMeter(InstrumentationOptions.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
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

    builder.Services.AddSupabaseJwtAuthentication(builder.Configuration, Log.Logger);
    builder.Services.AddAdminFeature();
    builder.Services.AddScoped<ISupabaseAdminAuthService, SupabaseAdminAuthService>();
    builder.Services.AddHostedService<IncompleteAccountSweeperWorker>();
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
        Supabase.Client supabaseClient = app.Services.GetRequiredService<Supabase.Client>();
        await supabaseClient.InitializeAsync();
    }

    app.Use(async (HttpContext context, Func<Task> next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/webhooks/supabase", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.ContentType = "application/json";
        }
        context.Request.EnableBuffering();
        await next();
    });

    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseCors("StrictFrontendPolicy");
    app.UseAuthentication();
    app.UseAuthorization();

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