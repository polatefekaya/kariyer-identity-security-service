namespace Kariyer.Identity.Domain.Entities;

public class LegacyEmployee
{
    public string Uid { get; private set; } = string.Empty;
    public Guid? ExternalId { get; private set; }
    public bool IsAccountCompleted { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public DateTime BirthDate { get; private set; }
    public List<string> SelectedSkills { get; private set; } = [];
    public List<string> Following { get; private set; } = [];
    public List<string> Followers { get; private set; } = [];
    public List<string> Notifications { get; private set; } = [];
    public List<string> Jwt { get; private set; } = [];

    public DateTimeOffset CreatedDate { get; private set; }
    public int OnboardingReminderStep { get; internal set; }

    protected LegacyEmployee() { }

    public static LegacyEmployee CreateFromExternalProvider(Guid externalId, string email, string phone, string firstName, string lastName)
    {
        return new LegacyEmployee
        {
            Uid = $"{externalId}-employee",
            ExternalId = externalId,
            IsAccountCompleted = false,
            Email = email,
            Phone = phone,
            Name = firstName,
            Surname = lastName,
            Username = $"{firstName.ToLower()}{lastName.ToLower()}{DateTimeOffset.UtcNow.Millisecond}",
            Password = string.Empty,
            BirthDate = DateTime.UtcNow.Date,
            CreatedDate = DateTimeOffset.UtcNow,
            OnboardingReminderStep = 0,
            SelectedSkills = [],
            Following = [],
            Followers = [],
            Notifications = [],
            Jwt = []
        };
    }
    
    public void LinkExternalAccount(Guid externalId)
    {
        ExternalId = externalId;
        IsAccountCompleted = true;
    }
}