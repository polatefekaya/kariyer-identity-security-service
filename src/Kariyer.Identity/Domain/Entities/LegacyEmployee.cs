namespace Kariyer.Identity.Domain.Entities;

public class LegacyEmployee
{
    // Primary Key (allowNull: false)
    public string Uid { get; set; } = string.Empty;
    public Guid? ExternalId { get; set; }
    
    // Core (allowNull: false)
    public bool IsAccountCompleted { get; set; }
    public string Email { get; set; } = string.Empty;
    public string LookingJob { get; set; } = "0"; // Default: '0'
    
    // Strings (allowNull: true)
    public string? Username { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BackgroundUrl { get; set; }
    public string? Name { get; set; }
    public string? Surname { get; set; }
    public string? Password { get; set; }
    public string? Phone { get; set; }
    public string? Race { get; set; }
    public string? NationalId { get; set; }
    public string? Gender { get; set; }
    public string? Country { get; set; }
    public string? Province { get; set; }
    public string? Town { get; set; }
    public string? Adress { get; set; }
    public string? Describe { get; set; }
    public string? Title { get; set; }
    public string? WorkingType { get; set; }
    public string? Neighbourhood { get; set; }
    
    // Dates (allowNull: true)
    public DateTimeOffset? BirthDate { get; set; }

    // Integers (allowNull: false)
    public int OnboardingReminderStep { get; set; }
    public int CompanyEmailOtpCount { get; set; }
    public int PasswordResetCount { get; set; }

    // Arrays (allowNull: false, defaultValue: [])
    public List<string> SelectedSkills { get; set; } = [];
    public List<string> Following { get; set; } = [];
    public List<string> Followers { get; set; } = [];
    public List<string> Notifications { get; set; } = [];
    public List<string> Jwt { get; set; } = [];

    // Booleans 
    public bool IsDeleted { get; set; }
    public bool PermaDeleted { get; set; }
    public bool IsRating { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public bool CompanyEmailVerified { get; set; }

    // Hashes (allowNull: true)
    public string? EmailHash { get; set; }
    public string? NationalIdHash { get; set; }
    public string? PhoneHash { get; set; }
    public string? PasswordHash { get; set; }

    // Audit & Verification (allowNull: true)
    public DateTimeOffset? EmailUpdate { get; set; }
    public DateTimeOffset? PhoneUpdate { get; set; }
    public DateTimeOffset? UsernameUpdate { get; set; }
    public string? CompanyEmail { get; set; }
    public string? VerificationCode { get; set; }
    public DateTimeOffset? VerificationCodeExpiresAt { get; set; }
    public DateTimeOffset? CompanyEmailOtpExpiredAt { get; set; }
    public DateTimeOffset? PasswordResetLastAttempt { get; set; }
    public DateTimeOffset? PasswordResetBlockedUntil { get; set; }
    public DateTimeOffset? DeletedDate { get; set; }
    public string? DeletedBy { get; set; }
    public DateTimeOffset? DeletionRequestedAt { get; set; }
    public string? OnesignalPlayerId { get; set; }

    // Created Date (allowNull: false)
    public DateTimeOffset CreatedDate { get; set; }

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
            BirthDate = DateTimeOffset.UtcNow.Date,
            CreatedDate = DateTimeOffset.UtcNow,
            OnboardingReminderStep = 0,
            LookingJob = "0",
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
        IsAccountCompleted = false;
    }
}