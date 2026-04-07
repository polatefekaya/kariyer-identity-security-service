using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Yarp.ReverseProxy.Transforms;
using Kariyer.Identity.Infrastructure.Persistence;
using Npgsql;
using Kariyer.Identity.Features.Webhooks.SyncExternalUser;
using Kariyer.Identity.Infrastructure.Gateway;
using Kariyer.Identity.Features.Account.AccountDidNotCompleted;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    
    string garnetConn = builder.Configuration.GetConnectionString("Garnet")
        ?? throw new InvalidOperationException("Garnet connection string missing.");

    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(garnetConn));
    
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("StrictFrontendPolicy", policy =>
        {
            policy.WithOrigins(
                "https://www.kariyerzamani.com",
                "https://auth.kariyerzamani.com",
                "https://kz-auth.kariyerzamani.com",
                "http://localhost:3000",
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
        .ConfigureResource(resource => resource.AddService("Kariyer.Identity"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddSource("MassTransit")
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("MassTransit")
            .AddOtlpExporter());

    string dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new ArgumentNullException("DefaultConnection is missing");

    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(dbConnectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
        }));

    builder.Services.AddMassTransit(busConfigurator =>
    {
        busConfigurator.SetKebabCaseEndpointNameFormatter();
        busConfigurator.AddConsumer<SyncExternalUserConsumer>();

        busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
        {
            string rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            rabbitConfigurator.Host(rabbitHost, "/", hostConfigurator =>
            {
                hostConfigurator.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                hostConfigurator.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            rabbitConfigurator.Message<ExternalUserCreatedEvent>(topology =>
            {
                topology.SetEntityName("identity.external.created");
            });
            
            rabbitConfigurator.Message<AccountDidNotCompletedEvent>(topology =>
            {
                topology.SetEntityName("identity.account.not-completed");
            });
            
            rabbitConfigurator.ReceiveEndpoint("external-user-created-queue", endpointConfigurator =>
            {
                endpointConfigurator.ConfigureConsumeTopology = false;

                endpointConfigurator.Bind("identity.external.created", bindConfigurator =>
                {
                    bindConfigurator.ExchangeType = "fanout";
                });

                endpointConfigurator.ConfigureConsumer<SyncExternalUserConsumer>(context);
            });
        });
    });

    string externalProviderUrl = builder.Configuration["ExternalProvider:Url"]
            ?? throw new ArgumentNullException("ExternalProvider:Url missing");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.Authority = $"{externalProviderUrl}/auth/v1";

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{externalProviderUrl}/auth/v1",
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Warning("JWT Validation Failed: {Message}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        });

    //builder.Services.AddAuthorization();
    
    builder.Services.AddHostedService<IncompleteAccountSweeperWorker>();
    builder.Services.AddCustomReverseProxy(builder.Configuration);
    
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebhookJsonContext.Default);
    });

    WebApplication app = builder.Build();

    app.Use(async (HttpContext context, Func<Task> next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/webhooks/supabase"))
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
    //app.UseAuthorization();

    app.MapReverseProxy();
    app.MapSyncSupabaseUserEndpoint();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
