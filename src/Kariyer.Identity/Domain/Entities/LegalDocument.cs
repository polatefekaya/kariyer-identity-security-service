namespace Kariyer.Identity.Domain.Entities;

public class LegalDocument
{
    public int Id { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string ApplicableTo { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool Required { get; set; }
    public string Title { get; set; } = string.Empty;
}
