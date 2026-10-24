namespace RBiz.Domain.Utilities;

/// <summary>
/// The states and union territories a company address may name, and the GST state codes
/// that go with them.
///
/// Held here rather than in a database table because the list changes only when the
/// country is reorganised, and a lookup table would mean a round trip on every address
/// validation for data that is effectively constant.
/// </summary>
public static class IndianStates
{
    /// <summary>
    /// GST state code to state name. The code is the first two digits of a GSTIN, so this
    /// doubles as the check that a GSTIN names a real state.
    /// </summary>
    private static readonly Dictionary<string, string> ByCode = new(StringComparer.Ordinal)
    {
        ["01"] = "Jammu and Kashmir",
        ["02"] = "Himachal Pradesh",
        ["03"] = "Punjab",
        ["04"] = "Chandigarh",
        ["05"] = "Uttarakhand",
        ["06"] = "Haryana",
        ["07"] = "Delhi",
        ["08"] = "Rajasthan",
        ["09"] = "Uttar Pradesh",
        ["10"] = "Bihar",
        ["11"] = "Sikkim",
        ["12"] = "Arunachal Pradesh",
        ["13"] = "Nagaland",
        ["14"] = "Manipur",
        ["15"] = "Mizoram",
        ["16"] = "Tripura",
        ["17"] = "Meghalaya",
        ["18"] = "Assam",
        ["19"] = "West Bengal",
        ["20"] = "Jharkhand",
        ["21"] = "Odisha",
        ["22"] = "Chhattisgarh",
        ["23"] = "Madhya Pradesh",
        ["24"] = "Gujarat",
        ["26"] = "Dadra and Nagar Haveli and Daman and Diu",
        ["27"] = "Maharashtra",
        ["29"] = "Karnataka",
        ["30"] = "Goa",
        ["31"] = "Lakshadweep",
        ["32"] = "Kerala",
        ["33"] = "Tamil Nadu",
        ["34"] = "Puducherry",
        ["35"] = "Andaman and Nicobar Islands",
        ["36"] = "Telangana",
        ["37"] = "Andhra Pradesh",
        ["38"] = "Ladakh"
    };

    private static readonly HashSet<string> Names =
        new(ByCode.Values, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every state and union territory, alphabetically. This is the list the address
    /// dropdown offers, so the API and the client agree on exactly one spelling each.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
        [.. ByCode.Values.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// True when the supplied name is one this system recognises. Case-insensitive, so a
    /// client that sends "gujarat" is not rejected over capitalisation.
    /// </summary>
    public static bool IsKnown(string? state) =>
        !string.IsNullOrWhiteSpace(state) && Names.Contains(state.Trim());

    /// <summary>
    /// Returns the canonical spelling for a recognised state, or null. Used to normalise
    /// what is stored so later grouping and reporting do not split on capitalisation.
    /// </summary>
    public static string? Canonicalise(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var trimmed = state.Trim();

        return All.FirstOrDefault(name => name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when the two digits are a GST state code in use.</summary>
    public static bool IsKnownStateCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ByCode.ContainsKey(code);

    /// <summary>The state a GST state code refers to, or null when the code is not in use.</summary>
    public static string? FromStateCode(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var name) ? name : null;
}
