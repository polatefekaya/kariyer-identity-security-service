namespace Kariyer.Identity.Features.Shared;

public static class AdminConstants
{
    public static readonly string[] AllowedEmailDomains = ["kariyerzamani.com", "psb-tech.com"];

    /// <summary>
    /// The admin sub-roles this platform recognises. Distinct from the Supabase
    /// account_type claim, which stays "admin" for every entry here.
    ///
    /// This list is validated on write because the API backend now treats some
    /// roles as path-scoped and denies by default. An unrecognised value such as
    /// "analitics" would therefore match no scope rule and lock the account out
    /// of everything, with nothing in the response explaining why. Rejecting it
    /// at creation time is far cheaper to diagnose.
    ///
    /// Keep in sync with validRoles in the API's adminManagementController.
    /// </summary>
    public static readonly string[] AllowedRoles = ["admin", "super_admin", "moderator", "analytics"];

    public static bool IsAllowedEmailDomain(string email)
    {
        int atIndex = email.LastIndexOf('@');
        if (atIndex < 0) return false;

        string domain = email[(atIndex + 1)..].ToLowerInvariant();
        return AllowedEmailDomains.Contains(domain);
    }

    public static bool IsAllowedRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return AllowedRoles.Contains(role.Trim().ToLowerInvariant());
    }

    public static string AllowedRolesDescription => string.Join(", ", AllowedRoles);
}
