namespace Kariyer.Identity.Features.Shared;

public static class AdminConstants
{
    public static readonly string[] AllowedEmailDomains = ["kariyerzamani.com", "psb-tech.com"];

    public static bool IsAllowedEmailDomain(string email)
    {
        int atIndex = email.LastIndexOf('@');
        if (atIndex < 0) return false;

        string domain = email[(atIndex + 1)..].ToLowerInvariant();
        return AllowedEmailDomains.Contains(domain);
    }
}
