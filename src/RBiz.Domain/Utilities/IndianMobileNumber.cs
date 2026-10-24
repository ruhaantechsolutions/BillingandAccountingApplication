namespace RBiz.Domain.Utilities;

/// <summary>
/// The mobile number rule used for signing in.
///
/// Ten digits beginning 6 to 9, which is the range Indian mobile numbers use. Held here so
/// registration, login and any later profile change all apply exactly one rule - a number
/// accepted at sign-up that login then rejects would lock someone out of their own account.
/// </summary>
public static class IndianMobileNumber
{
    public const int Length = 10;

    /// <summary>
    /// Strips spaces, hyphens and a +91 or 0 prefix, so a number pasted from a contact card
    /// is accepted rather than refused on formatting the caller cannot see.
    /// </summary>
    public static string Normalise(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return string.Empty;
        }

        Span<char> digits = stackalloc char[16];
        var count = 0;

        foreach (var ch in mobile)
        {
            if (char.IsAsciiDigit(ch) && count < digits.Length)
            {
                digits[count++] = ch;
            }
        }

        var value = new string(digits[..count]);

        // Country code or a trunk prefix in front of the ten digits we keep.
        if (value.Length == 12 && value.StartsWith("91", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (value.Length == 11 && value[0] == '0')
        {
            value = value[1..];
        }

        return value;
    }

    /// <summary>True when the normalised value is a usable Indian mobile number.</summary>
    public static bool IsValid(string? mobile)
    {
        var value = Normalise(mobile);

        return value.Length == Length && value[0] is >= '6' and <= '9';
    }
}
