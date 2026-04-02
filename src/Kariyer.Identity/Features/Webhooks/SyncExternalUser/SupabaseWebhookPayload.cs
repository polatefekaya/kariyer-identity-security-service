using System.Text.Json.Serialization;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public record SupabaseAuthHookPayload(
    [property: JsonPropertyName("metadata")] AuthHookMetadata? Metadata,
    [property: JsonPropertyName("user")] SupabaseAuthUser? User
);

public record AuthHookMetadata(
    [property: JsonPropertyName("name")] string? Name
);

public record SupabaseAuthUser(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("user_metadata")] SupabaseUserMetadata? UserMetadata
);

public record SupabaseUserMetadata(
    [property: JsonPropertyName("account_type")] string? AccountType,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber,
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl
);

[JsonSerializable(typeof(SupabaseAuthHookPayload))]
[JsonSerializable(typeof(AuthHookMetadata))]
[JsonSerializable(typeof(SupabaseAuthUser))]
[JsonSerializable(typeof(SupabaseUserMetadata))]
public partial class WebhookJsonContext : JsonSerializerContext
{
}