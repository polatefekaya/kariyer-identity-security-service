namespace Kariyer.Identity.Domain.Entities;

public class LegalConsentLog
{
    public int Id { get; set; }
    public string UserUid { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public int LegalDocId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string DocVersion { get; set; } = string.Empty;
    public bool Accepted { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public string? IpAddress { get; set; }
}
