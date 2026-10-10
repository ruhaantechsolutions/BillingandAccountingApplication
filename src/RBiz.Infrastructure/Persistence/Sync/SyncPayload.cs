using System.Text.Json;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// Reads values out of a loosely-typed client payload.
///
/// The payload arrives as a dictionary of JsonElement, because each provider decides
/// which fields it will accept. These helpers keep that parsing in one place so a
/// provider never has to think about how JSON happened to represent a value.
/// </summary>
internal static class SyncPayload
{
    /// <summary>
    /// Reads a boolean, accepting a real JSON boolean or the strings "true"/"false".
    /// </summary>
    public static bool TryReadBoolean(object? value, out bool result)
    {
        result = false;

        switch (value)
        {
            case bool boolean:
                result = boolean;
                return true;

            case JsonElement { ValueKind: JsonValueKind.True }:
                result = true;
                return true;

            case JsonElement { ValueKind: JsonValueKind.False }:
                return true;

            case JsonElement { ValueKind: JsonValueKind.String } element:
                return bool.TryParse(element.GetString(), out result);

            case string text:
                return bool.TryParse(text, out result);

            default:
                return false;
        }
    }

    /// <summary>
    /// Reads a string, treating JSON null and whitespace alike as absent.
    /// </summary>
    public static bool TryReadString(object? value, out string result)
    {
        result = string.Empty;

        var text = value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            string raw => raw,
            null => null,
            _ => value.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        result = text.Trim();
        return true;
    }
}
