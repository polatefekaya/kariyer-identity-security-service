using System.Text;

namespace Kariyer.Identity.Features.Shared;

/// <summary>
/// Canonical phone shape for the platform: the bare 10-digit Turkish local number
/// (<c>5XXXXXXXXX</c>) — no country code, no leading zero, no separators.
/// <para>
/// This mirrors <c>kariyer_zamani_backend/src/utils/phoneUtil.js</c> and the auth-hub
/// registration form. Every writer must agree, otherwise the same human number is
/// stored in several shapes and <c>phone_hash</c> lookups stop matching across services.
/// </para>
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Reduces any accepted input shape (+90…, 0090…, 90…, 0…, spaced, parenthesised)
    /// to the canonical 10 digits. Returns an empty string when nothing usable remains.
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        StringBuilder builder = new(phone.Length);
        foreach (char c in phone)
        {
            if (char.IsAsciiDigit(c)) builder.Append(c);
        }

        string digits = builder.ToString();
        if (digits.Length == 0) return string.Empty;

        // 00 90 5XX… (international dialling prefix)
        if (digits.Length == 14 && digits.StartsWith("0090", StringComparison.Ordinal))
            digits = digits[4..];

        // 90 5XX… (country code)
        if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal))
            digits = digits[2..];

        // 0 5XX… (national trunk prefix)
        if (digits.Length == 11 && digits[0] == '0')
            digits = digits[1..];

        // Anything still longer: keep the trailing 10 digits.
        if (digits.Length > 10)
            digits = digits[^10..];

        return digits;
    }

    /// <summary>True when the value normalizes to a well-formed Turkish mobile number.</summary>
    public static bool IsValid(string? phone)
    {
        string normalized = Normalize(phone);
        return normalized.Length == 10 && normalized[0] == '5';
    }
}
