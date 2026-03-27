using System;
using System.Text.Json.Serialization;

namespace Kariyer.Identity.Features.Webhooks.SyncSupabaseUser;

public record SupabaseWebhookPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("record")] SupabaseRecord Record
);

public record SupabaseRecord(
    [property: JsonPropertyName("id")] Ulid Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("raw_user_meta_data")] UserMetaData MetaData
);

public record UserMetaData(
    [property: JsonPropertyName("account_type")] string AccountType,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("phone_number")] string PhoneNumber
);

[JsonSerializable(typeof(SupabaseWebhookPayload))]
public partial class WebhookJsonContext : JsonSerializerContext
{
}