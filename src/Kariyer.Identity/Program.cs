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
using Kariyer.Identity.Features.Webhooks.SyncSupabaseUser;
using Npgsql;
using Kariyer.Identity.Features.Webhooks.SyncExternalUser;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("StrictFrontendPolicy", policy =>
        {
            policy.WithOrigins(
                    "https://www.kariyerzamani.com",
                    "https://auth.kariyerzamani.com",
                    "http://localhost:3000",
                    "http://localhost:5173",
                    "http://kariyerzamani.com",
                    "http://admin.kariyerzamani.com",
                    "http://tst.kariyerzamani.com"
            )
                  .AllowAnyMethod()
                  .WithHeaders("Authorization", "Content-Type", "Accept")
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

            rabbitConfigurator.ConfigureEndpoints(context);
        });
    });

    string externalProviderUrl = builder.Configuration["ExternalProvider:Url"] ?? throw new ArgumentNullException("ExternalProvider:Url missing");
    string externalProviderSecret = builder.Configuration["ExternalProvider:JwtSecret"] ?? throw new ArgumentNullException("ExternalProvider:JwtSecret missing");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"{externalProviderUrl}/auth/v1";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidAudience = "authenticated",
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(externalProviderSecret))
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms(builderContext =>
        {
            builderContext.AddRequestTransform(async transformContext =>
            {
                var user = transformContext.HttpContext.User;
                
                if (user.Identity != null && user.Identity.IsAuthenticated)
                {
                    string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    string? email = user.FindFirst(ClaimTypes.Email)?.Value;
                    
                    string role = user.FindFirst("account_type")?.Value ?? "candidate"; 
    
                    if (userId != null) transformContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                    if (email != null) transformContext.ProxyRequest.Headers.Add("X-User-Email", email);
                    transformContext.ProxyRequest.Headers.Add("X-User-Role", role);
                }
                else
                {
                    transformContext.ProxyRequest.Headers.Add("X-User-Role", "guest");
                }
                
                await ValueTask.CompletedTask;
            });
        });

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebhookJsonContext.Default);
    });

    WebApplication app = builder.Build();
    
    app.Use(async (HttpContext context, Func<Task> next) =>
    {
        context.Request.EnableBuffering();
        await next();
    });
    
    app.UseSerilogRequestLogging();
    app.UseCors("StrictFrontendPolicy");
    app.UseAuthentication();
    app.UseAuthorization();

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