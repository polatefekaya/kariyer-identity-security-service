using Kariyer.Identity.Domain.Enums;

namespace Kariyer.Identity.Domain.Entities;

public class LegacyCompany
{
    public string Uid { get; set; } = string.Empty;
    
    public Guid? ExternalId { get; set; }
    public bool IsAccountCompleted { get; set; }
    
    public string Email { get; set; } = string.Empty;
    
    public string? Status { get; set; }
    public string? Username { get; set; }
    public string? CompanyName { get; set; }
    public string? AuthorizedName { get; set; }
    public string? AuthorizedSurname { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? Province { get; set; }
    public string? Town { get; set; }
    public string? Neighbourhood { get; set; }
    public string? Location { get; set; }
    public string? TaxOfficeProvince { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxIdNumber { get; set; }
    public string? Password { get; set; }
    public string? MailDomain { get; set; }
    public string? FoundedYear { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? Industry { get; set; }
    public string? CompanyDesc { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BackgroundUrl { get; set; }
    public string? TaxCertificateUrl { get; set; }
    public string? EmployeeCount { get; set; }
    
    // Booleans (allowNull: false)
    public bool IsCivilian { get; set; }
    public bool EmploymentOffice { get; set; }
    public bool IsDeleted { get; set; }
    public bool PermaDeleted { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }

    public bool KvkkIsverenAccepted { get; set; }
    public bool IsverenSozlesmesiAccepted { get; set; }
    public bool TicariElektronikIletiAccepted { get; set; }

    // Integers (allowNull: false)
    public int OnboardingReminderStep { get; set; }
    public int PriorityScore { get; set; }
    public int PasswordResetCount { get; set; }

    // Arrays (allowNull: false, defaultValue: [])
    // Npgsql natively maps PostgreSQL TEXT[] to List<string>
    public List<string> Followers { get; set; } = [];
    public List<string> SubCompanies { get; set; } = [];
    public List<string> ParentCompanies { get; set; } = [];
    public List<string> AffiliatedCompanies { get; set; } = [];
    public List<string> PendingSentRequests { get; set; } = [];
    public List<string> PendingReceivedRequests { get; set; } = [];
    public List<string> Employees { get; set; } = [];
    public List<string> ApprovedEmployees { get; set; } = [];
    public List<string> RejectedEmployees { get; set; } = [];
    public List<string> DeletedEmployees { get; set; } = [];
    public List<string> Jwt { get; set; } = [];
    public List<string> Plan { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> Notifications { get; set; } = [];
    public List<int> SeenCv { get; set; } = [];
    public List<int> DownloadedCv { get; set; } = [];

    // Hashes & Socials (allowNull: true)
    public string? TaxIdNumberHash { get; set; }
    public string? EmailHash { get; set; }
    public string? PhoneHash { get; set; }
    public string? PasswordHash { get; set; }
    public string? Instagram { get; set; }
    public string? InstagramUrl { get; set; }
    public string? Linkedin { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? Twitter { get; set; }
    public string? TwitterUrl { get; set; }

    // Dates & Extras (allowNull: true)
    public string? VerificationCode { get; set; }
    public DateTimeOffset? VerificationCodeExpiresAt { get; set; }
    public DateTimeOffset? PasswordResetLastAttempt { get; set; }
    public DateTimeOffset? PasswordResetBlockedUntil { get; set; }
    public DateTimeOffset? EmailUpdate { get; set; }
    public DateTimeOffset? PhoneUpdate { get; set; }
    public DateTimeOffset? UsernameUpdate { get; set; }
    public DateTimeOffset? DeletedDate { get; set; }
    public string? DeletedBy { get; set; }
    public DateTimeOffset? DeletionRequestedAt { get; set; }
    public string? OnesignalPlayerId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    
    public ApprovedStatus Approved { get; set; } = ApprovedStatus.Registered;
    
    public DateTimeOffset CreatedDate { get; set; }

    protected LegacyCompany() { }

    public static LegacyCompany CreateFromExternalProvider(Guid externalId, string email, string phone, string firstName, string lastName, bool kvkkIsverenAccepted, bool isverenSozlesmesiAccepted, bool ticariElektronikIletiAccepted)
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
            Status = "pending_approval",
            Username = $"{firstName.ToLower()}{lastName.ToLower()}{DateTimeOffset.UtcNow.Millisecond}",
            Password = string.Empty,
            CreatedDate = DateTimeOffset.UtcNow,
            OnboardingReminderStep = 0,
            KvkkIsverenAccepted = kvkkIsverenAccepted,
            IsverenSozlesmesiAccepted = isverenSozlesmesiAccepted,
            TicariElektronikIletiAccepted = ticariElektronikIletiAccepted,
        };
    }
    
    public void LinkExternalAccount(Guid externalId)
    {
        ExternalId = externalId;
        IsAccountCompleted = false;
    }

    public void Freeze(string deletedBy)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedDate = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedBy = null;
        DeletedDate = null;
        DeletionRequestedAt = null;
    }

    public void AnonymizeForDeletion(string externalIdSuffix)
    {
        if (PermaDeleted) return;

        const string delMarker = "__del_";
        string suffix = $"{delMarker}{externalIdSuffix}";

        if (!Email.Contains(delMarker)) Email = Email + suffix;
        if (Phone != null && !Phone.Contains(delMarker)) Phone = Phone + suffix;
        if (Username != null && !Username.Contains(delMarker)) Username = Username + suffix;
        if (TaxIdNumber != null && !TaxIdNumber.Contains(delMarker)) TaxIdNumber = TaxIdNumber + suffix;

        // Hashes are no longer valid lookup keys
        EmailHash = null;
        PhoneHash = null;
        TaxIdNumberHash = null;

        IsDeleted = true;
        PermaDeleted = true;
        DeletedDate = DateTimeOffset.UtcNow;
    }

    public void UpdateEmail(string newEmail, string? newEmailHash)
    {
        Email = newEmail;
        EmailHash = newEmailHash;
        EmailVerified = false;
        EmailUpdate = DateTimeOffset.UtcNow;
    }

    public void RevertEmail(string oldEmail, string? oldEmailHash)
    {
        Email = oldEmail;
        EmailHash = oldEmailHash;
        EmailUpdate = DateTimeOffset.UtcNow;
    }

    public void UpdatePhone(string? newPhone, string? newPhoneHash)
    {
        Phone = newPhone;
        PhoneHash = newPhoneHash;
        PhoneVerified = false;
        PhoneUpdate = DateTimeOffset.UtcNow;
    }

    public void RevertPhone(string? oldPhone, string? oldPhoneHash)
    {
        Phone = oldPhone;
        PhoneHash = oldPhoneHash;
        PhoneUpdate = DateTimeOffset.UtcNow;
    }

    public void UpdateUsername(string? newUsername)
    {
        Username = newUsername;
        UsernameUpdate = DateTimeOffset.UtcNow;
    }
}