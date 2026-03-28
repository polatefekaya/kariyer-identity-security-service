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
            logger.LogInformation("Webhook received. Preparing to publish to RabbitMQ.");
            
            if (payload.Type != "INSERT") 
            {
                return Results.Json(new { }, contentType: "application/json");
            }

            ExternalUserCreatedEvent integrationEvent = new()
            {
                UserId = payload.Record.Id,
                Email = payload.Record.Email ?? string.Empty,
                AccountType = payload.Record.MetaData.AccountType ?? "c",
                FirstName = payload.Record.MetaData.FirstName ?? string.Empty,
                LastName = payload.Record.MetaData.LastName ?? string.Empty,
                PhoneNumber = payload.Record.MetaData.PhoneNumber ?? string.Empty
            };

            logger.LogInformation("Integration Event publishing. AType: {type}, Id: {id}, Email: {email}", integrationEvent.AccountType, integrationEvent.UserId, integrationEvent.Email);

            await publishEndpoint.Publish(integrationEvent, cancellationToken);
            
            return Results.Json(new { }, contentType: "application/json");
        }).AddEndpointFilter<SupabaseSignatureFilter>();
    }
}