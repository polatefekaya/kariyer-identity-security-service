using Kariyer.Identity.Features.Webhooks.SyncSupabaseUser;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public static class SyncExternalUserEndpoint
{
    public static void MapSyncSupabaseUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/supabase/user-created", async (
            [FromBody] SupabaseWebhookPayload payload,
            IPublishEndpoint publishEndpoint,
            ILogger<SupabaseSignatureFilter> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Webhook received. Type: '{Type}', Record ID: '{Id}'", 
                payload.Type ?? "NULL", 
                payload.Record?.Id ?? null);

            if (!string.Equals(payload.Type, "INSERT", StringComparison.OrdinalIgnoreCase)) 
            {
                logger.LogWarning("Webhook aborted. Expected Type 'INSERT', got '{Type}'.", payload.Type);
                return Results.Json(new { }, contentType: "application/json");
            }

            if (payload.Record == null)
            {
                logger.LogError("Webhook payload contained no Record data.");
                return Results.BadRequest("Missing Record data.");
            }

            try
            {
                ExternalUserCreatedEvent integrationEvent = new()
                {
                    UserId = payload.Record.Id,
                    Email = payload.Record.Email ?? string.Empty,
                    AccountType = payload.Record.MetaData?.AccountType ?? "candidate",
                    FirstName = payload.Record.MetaData?.FirstName ?? string.Empty,
                    LastName = payload.Record.MetaData?.LastName ?? string.Empty,
                    PhoneNumber = payload.Record.MetaData?.PhoneNumber ?? string.Empty
                };

                logger.LogInformation("Integration Event publishing. AType: {type}, Id: {id}, Email: {email}", 
                    integrationEvent.AccountType, integrationEvent.UserId, integrationEvent.Email);

                await publishEndpoint.Publish(integrationEvent, cancellationToken);
                
                logger.LogInformation("Message successfully handed off to MassTransit.");
                return Results.Json(new { }, contentType: "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Failed to map payload or publish to RabbitMQ.");
                return Results.Problem("Internal Message Broker Failure");
            }
        }).AddEndpointFilter<SupabaseSignatureFilter>();
    }
}