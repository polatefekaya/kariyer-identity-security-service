using System.Text;

namespace Kariyer.Identity.Features.Shared;

/// <summary>
/// Builds the auto-assigned username for accounts created by the Supabase webhook.
/// <para>
/// The web app enforces <c>/^[a-zA-Z0-9_]+$/</c> on usernames (see
/// <c>EmployeeOnboarding.tsx</c> <c>toUsername</c>), so this folds Turkish diacritics
/// and strips everything else — otherwise identity mints values like
/// <c>çağrıöztürk123</c> that the user can never edit without a full rename.
/// </para>
/// </summary>
public static class UsernameGenerator
{
    private const int MaxLength = 30;
    private const int SuffixLength = 8;
    private const string EmptyNameFallback = "kz";

    /// <summary>
    /// <paramref name="externalId"/> supplies the uniqueness suffix — a millisecond
    /// component only spans 0-999 and collides readily under concurrent signups.
    /// </summary>
    public static string Generate(string? firstName, string? lastName, Guid externalId)
    {
        string baseName = Fold($"{firstName}{lastName}");
        if (baseName.Length == 0) baseName = EmptyNameFallback;

        string suffix = externalId.ToString("N")[..SuffixLength];

        int room = MaxLength - suffix.Length;
        if (baseName.Length > room) baseName = baseName[..room];

        return baseName + suffix;
    }

    private static string Fold(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char c in value)
        {
            char folded = c switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(c),
            };

            if (char.IsAsciiLetterLower(folded) || char.IsAsciiDigit(folded) || folded == '_')
            {
                builder.Append(folded);
            }
        }

        return builder.ToString();
    }
}
