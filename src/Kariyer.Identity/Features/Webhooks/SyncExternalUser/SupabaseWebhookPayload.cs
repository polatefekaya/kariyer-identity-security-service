using System.Text.Json.Serialization;

namespace Kariyer.Identity.Features.Webhooks.SyncExternalUser;

public sealed record DatabaseWebhookPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("table")] string Table,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("record")] AuthUserRecord? Record,
    [property: JsonPropertyName("old_record")] AuthUserRecord? OldRecord
);

public sealed record AuthUserRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("raw_app_meta_data")] SupabaseAppMetadata? AppMetadata,
    [property: JsonPropertyName("raw_user_meta_data")] SupabaseUserMetadata? UserMetadata
);

public sealed record SupabaseAppMetadata(
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("providers")] string[]? Providers
);

public sealed record SupabaseUserMetadata(
    [property: JsonPropertyName("account_type")] string? AccountType,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber,
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    // Employee-specific consent fields
    [property: JsonPropertyName("kvkk_aydinlatma_accepted")] bool KvkkAydinlatmaAccepted,
    [property: JsonPropertyName("kullanici_sozlesmesi_accepted")] bool KullaniciSozlesmesiAccepted,
    [property: JsonPropertyName("acik_riza_accepted")] bool AcikRizaAccepted,
    // Company-specific consent fields
    [property: JsonPropertyName("kvkk_isveren_accepted")] bool KvkkIsverenAccepted,
    [property: JsonPropertyName("isveren_sozlesmesi_accepted")] bool IsverenSozlesmesiAccepted,
    // Shared consent field (employee + company, optional)
    [property: JsonPropertyName("ticari_elektronik_ileti_accepted")] bool TicariElektronikIletiAccepted
);

[JsonSerializable(typeof(DatabaseWebhookPayload))]
[JsonSerializable(typeof(AuthUserRecord))]
[JsonSerializable(typeof(SupabaseAppMetadata))]
[JsonSerializable(typeof(SupabaseUserMetadata))]
public partial class WebhookJsonContext : JsonSerializerContext
{
}