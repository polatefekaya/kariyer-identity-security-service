using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public static class SyncExternalUserEndpoint
{
    public static void MapSyncSupabaseUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/supabase/user-created", async (
            [FromBody] SupabaseAuthHookPayload payload,
            IPublishEndpoint publishEndpoint,
            ILogger<SupabaseSignatureFilter> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Webhook received. Hook Name: '{Name}', User ID: '{Id}'", 
                payload.Metadata?.Name ?? "NULL", 
                payload.User?.Id.ToString() ?? "NULL");

            if (!string.Equals(payload.Metadata?.Name, "before-user-created", StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(payload.Metadata?.Name, "after-user-created", StringComparison.OrdinalIgnoreCase)) 
            {
                logger.LogWarning("Webhook aborted. Expected user creation hook, got '{Name}'.", payload.Metadata?.Name);
                return Results.Json(new { }, contentType: "application/json");
            }

            if (payload.User == null)
            {
                logger.LogError("Webhook payload contained no User data.");
                return Results.BadRequest("Missing User data.");
            }

            try
            {
                ExternalUserCreatedEvent integrationEvent = new()
                {
                    UserId = payload.User.Id,
                    Email = payload.User.Email ?? string.Empty,
                    AccountType = payload.User.UserMetadata?.AccountType ?? "candidate",
                    FirstName = payload.User.UserMetadata?.FirstName ?? string.Empty,
                    LastName = payload.User.UserMetadata?.LastName ?? string.Empty,
                    PhoneNumber = payload.User.UserMetadata?.PhoneNumber ?? string.Empty
                };

                logger.LogInformation("Integration Event publishing. AType: {type}, Id: {id}, Email: {email}", 
                    integrationEvent.AccountType, integrationEvent.UserId, integrationEvent.Email);

                await publishEndpoint.Publish(integrationEvent, cancellationToken);
                
                logger.LogInformation("Message successfully handed off to MassTransit.");
                return Results.Json(new { }, contentType: "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Failed to map Auth Hook payload or publish to RabbitMQ.");
                return Results.Problem("Internal Message Broker Failure");
            }
        }).AddEndpointFilter<SupabaseSignatureFilter>();
    }
}