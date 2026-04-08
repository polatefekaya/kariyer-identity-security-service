namespace Kariyer.Identity.Domain.Entities;

public class LegacyAdmin
{
    public string Uid { get; private set; } = string.Empty;
    public Guid? ExternalId { get; private set; }
    public bool IsAccountCompleted { get; private set; }
    
    public string? Email { get; private set; }
    public string? Username { get; private set; }
    public string? Name { get; private set; }
    public string? Surname { get; private set; }
    public string? Phone { get; private set; }
    public string? Race { get; private set; }
    public DateTime? BirthDate { get; private set; }
    public string? Country { get; private set; }
    public string? Province { get; private set; }
    public string? Town { get; private set; }
    public string? Neighbourhood { get; private set; }
    public string? NationalIdHash { get; private set; }
    public string? PasswordHash { get; private set; }
    
    public string? PhotoUrl { get; private set; }
    public string? BackgroundUrl { get; private set; }
    public string? Gender { get; private set; }
    public string? Address { get; private set; }
    public string? Title { get; private set; }
    public string? AdminRole { get; private set; }
    
    public string? EmailHash { get; private set; }
    public string? PhoneHash { get; private set; }
    
    public string[]? Notifications { get; private set; }
    public bool IsDeleted { get; private set; }
    public bool PermaDeleted { get; private set; }
    public bool IsActive { get; private set; }
    public string[]? Jwt { get; private set; }
    
    public bool EmailVerified { get; private set; }
    public bool PhoneVerified { get; private set; }
    public DateTime? CreatedDate { get; private set; }
    public DateTime? LastLogin { get; private set; }
    public DateTime? EmailUpdate { get; private set; }
    public DateTime? PhoneUpdate { get; private set; }
    public DateTime? UsernameUpdate { get; private set; }
    public DateTime? PasswordUpdate { get; private set; }
    public string? VerificationCode { get; private set; }
    public DateTime? VerificationCodeExpiresAt { get; private set; }
    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetExpiresAt { get; private set; }

    protected LegacyAdmin() { }

    public static LegacyAdmin CreateFromExternalProvider(Guid externalId, string email, string phone, string name, string surname, string role)
    {
        return new LegacyAdmin
        {
            Uid = $"{Guid.NewGuid()}-admin",
            ExternalId = externalId,
            Email = email,
            Phone = phone,
            Name = name,
            Surname = surname,
            AdminRole = string.IsNullOrWhiteSpace(role) || role == "a" ? "admin" : role,
            IsAccountCompleted = false,
            IsActive = true,
            IsDeleted = false,
            PermaDeleted = false,
            EmailVerified = false,
            PhoneVerified = false,
            CreatedDate = DateTime.UtcNow
        };
    }
    
    public void CompleteAccount(){
        IsAccountCompleted = true;
    }
    
    public void UpdateRole(string newRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newRole, "AdminRole");
        AdminRole = newRole;
    }

    public void UpdateIsActive(bool isActive){
        IsActive = isActive;
    }
    
    public void LinkExternalAccount(Guid externalId)
    {
        ExternalId = externalId;
        IsAccountCompleted = true;
    }
}