using System.Text.RegularExpressions;

namespace RBiz.Domain.Utilities;

/// <summary>Why a GSTIN was rejected, or that it passed every offline check.</summary>
public enum GstinValidationResult
{
    Valid = 0,

    /// <summary>Nothing was supplied.</summary>
    Empty = 1,

    /// <summary>Not 15 characters in the shape a GSTIN takes.</summary>
    FormatInvalid = 2,

    /// <summary>The leading two digits are not a state code in use.</summary>
    UnknownStateCode = 3,

    /// <summary>
    /// Shaped correctly but the final check digit does not match the rest of the number,
    /// which means it was mistyped rather than merely malformed.
    /// </summary>
    ChecksumInvalid = 4
}

/// <summary>
/// Offline validation of a GSTIN: shape, state code, embedded PAN and check digit.
///
/// This is deliberately the whole of what can be checked without a network call. There is
/// no free, openly licensed service that confirms a GSTIN against the live GST registry -
/// the government API is reached through a licensed GSP - so proving a number is
/// *well-formed* and proving it is *registered* are two different things. The first is
/// done here; the second is left to <c>IGstinVerificationService</c>, and the two outcomes
/// are reported separately so an unverifiable number is never confused with an invalid one.
///
/// The check digit is the part that earns its keep: a regex accepts any 15 characters of
/// the right shape, while the check digit catches the single mistyped character that is by
/// far the most common real error.
/// </summary>
public static partial class GstinValidator
{
    public const int GstinLength = 15;

    /// <summary>
    /// 2 digits state code, 10 character PAN, 1 entity code, a literal Z, 1 check digit.
    /// </summary>
    [GeneratedRegex(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.CultureInvariant)]
    private static partial Regex GstinPattern { get; }

    /// <summary>
    /// The alphabet GSTIN check digits are computed over: a character's value is its
    /// position here, so '0' is 0, 'A' is 10 and 'Z' is 35.
    /// </summary>
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Normalises a candidate the way it will be stored: trimmed and upper-cased. Returns
    /// an empty string for null or blank input.
    /// </summary>
    public static string Normalise(string? gstin) =>
        string.IsNullOrWhiteSpace(gstin) ? string.Empty : gstin.Trim().ToUpperInvariant();

    /// <summary>
    /// Runs every offline check in order, so the caller can tell the user precisely what
    /// is wrong rather than only that something is.
    /// </summary>
    public static GstinValidationResult Validate(string? gstin)
    {
        var value = Normalise(gstin);

        if (value.Length == 0)
        {
            return GstinValidationResult.Empty;
        }

        if (value.Length != GstinLength || !GstinPattern.IsMatch(value))
        {
            return GstinValidationResult.FormatInvalid;
        }

        if (!IndianStates.IsKnownStateCode(value[..2]))
        {
            return GstinValidationResult.UnknownStateCode;
        }

        return ComputeCheckDigit(value) == value[^1]
            ? GstinValidationResult.Valid
            : GstinValidationResult.ChecksumInvalid;
    }

    /// <summary>Convenience for callers that only need a yes or no.</summary>
    public static bool IsValid(string? gstin) => Validate(gstin) is GstinValidationResult.Valid;

    /// <summary>
    /// The state code a well-formed GSTIN carries, or null. Lets a caller point out that a
    /// registration belongs to a different state from the address, without rejecting it -
    /// a business may legitimately register in a state it is not headquartered in.
    /// </summary>
    public static string? GetStateName(string? gstin)
    {
        var value = Normalise(gstin);

        return value.Length == GstinLength ? IndianStates.FromStateCode(value[..2]) : null;
    }

    /// <summary>
    /// The PAN embedded at characters 3 to 12. Useful later for matching a company's GST
    /// registrations to one legal entity.
    /// </summary>
    public static string? GetPan(string? gstin)
    {
        var value = Normalise(gstin);

        return value.Length == GstinLength ? value.Substring(2, 10) : null;
    }

    /// <summary>
    /// The published GSTIN check-digit algorithm, over the first 14 characters.
    ///
    /// Each character's value is weighted by 1 or 2 alternately; the product's quotient and
    /// remainder against 36 are both added to the running total. The check digit is
    /// whatever brings that total up to the next multiple of 36.
    /// </summary>
    private static char ComputeCheckDigit(string gstin)
    {
        var sum = 0;

        for (var i = 0; i < GstinLength - 1; i++)
        {
            var value = Alphabet.IndexOf(gstin[i], StringComparison.Ordinal);

            // Unreachable for a string that matched the pattern, but a negative index here
            // would silently corrupt the total rather than fail, so it is worth the guard.
            if (value < 0)
            {
                return '\0';
            }

            var product = value * (i % 2 == 0 ? 1 : 2);

            sum += (product / Alphabet.Length) + (product % Alphabet.Length);
        }

        return Alphabet[(Alphabet.Length - (sum % Alphabet.Length)) % Alphabet.Length];
    }
}
