namespace Kariyer.Identity.Domain.Entities;

public class LegacyCompany
{
    public string Uid { get; private set; } = string.Empty;
    public Guid? ExternalId { get; private set; }
    public bool IsAccountCompleted { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string CompanyName { get; private set; } = string.Empty;
    public string AuthorizedName { get; private set; } = string.Empty;
    public string AuthorizedSurname { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    
    // Arrays required by legacy code
    public List<string> Followers { get; private set; } = [];
    public List<string> SubCompanies { get; private set; } = [];
    public List<string> ParentCompanies { get; private set; } = [];
    public List<string> AffiliatedCompanies { get; private set; } = [];
    public List<string> PendingSentRequests { get; private set; } = [];
    public List<string> PendingReceivedRequests { get; private set; } = [];
    public List<string> Employees { get; private set; } = [];
    public List<string> ApprovedEmployees { get; private set; } = [];
    public List<string> RejectedEmployees { get; private set; } = [];
    public List<string> DeletedEmployees { get; private set; } = [];
    public List<string> Jwt { get; private set; } = [];
    public List<string> Plan { get; private set; } = [];
    public List<string> Tags { get; private set; } = [];
    public List<int> SeenCv { get; private set; } = [];
    public List<int> DownloadedCv { get; private set; } = [];
    public List<string> Notifications { get; private set; } = [];
    public DateTimeOffset CreatedDate { get; private set; }
    public int OnboardingReminderStep { get; internal set; }

    protected LegacyCompany() { }

    public static LegacyCompany CreateFromExternalProvider(Guid externalId, string email, string phone, string firstName, string lastName)
    {
        return new LegacyCompany
        {
            Uid = $"{externalId}-company",
            ExternalId = externalId,
            IsAccountCompleted = false,
            Email = email,
            Phone = phone,
            CompanyName = $"{firstName} {lastName} Şirketi",
            AuthorizedName = firstName,
            AuthorizedSurname = lastName,
            Username = $"{firstName.ToLower()}{lastName.ToLower()}{DateTimeOffset.UtcNow.Millisecond}",
            Password = string.Empty,
            CreatedDate = DateTimeOffset.UtcNow,
            OnboardingReminderStep = 0
        };
    }
    
    public void LinkExternalAccount(Guid externalId)
    {
        ExternalId = externalId;
        IsAccountCompleted = true;
    }
}