using System.Buffers.Binary;

namespace RBiz.Application.Sync;

/// <summary>
/// Converts an 8-byte row version into an opaque text cursor and back.
///
/// Clients are given a string rather than the raw bytes on purpose: the cursor is a
/// position marker, not data to interpret. Keeping it opaque means the underlying change
/// marker can be replaced later (a sequence column, a logical clock) without breaking
/// every stored client cursor.
/// </summary>
public static class SyncCursor
{
    /// <summary>A SQL Server rowversion is always exactly 8 bytes.</summary>
    public const int RowVersionLength = 8;

    /// <summary>
    /// Cursor meaning "I have nothing yet, send me everything". Represented as the
    /// all-zero row version, which sorts below every real row.
    /// </summary>
    public static byte[] Beginning => new byte[RowVersionLength];

    /// <summary>
    /// Encodes a row version as a URL-safe string, or null when there is nothing to encode.
    /// </summary>
    public static string? Encode(byte[]? rowVersion) =>
        rowVersion is null || rowVersion.Length == 0
            ? null
            : Convert.ToBase64String(rowVersion)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

    /// <summary>
    /// Decodes a client-supplied cursor. A null or blank cursor means "from the
    /// beginning"; anything present but malformed is rejected rather than silently
    /// treated as the beginning, because that would quietly re-download the world.
    /// </summary>
    public static bool TryDecode(string? cursor, out byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            rowVersion = Beginning;
            return true;
        }

        var padded = cursor.Replace('-', '+').Replace('_', '/');

        // Base64 needs its length to be a multiple of four; the padding was trimmed off
        // to keep the cursor tidy in a query string.
        padded = (cursor.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            0 => padded,
            _ => padded
        };

        rowVersion = Beginning;

        if (cursor.Length % 4 == 1)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[RowVersionLength];

        if (!Convert.TryFromBase64String(padded, buffer, out var written) ||
            written != RowVersionLength)
        {
            return false;
        }

        rowVersion = buffer.ToArray();
        return true;
    }

    /// <summary>
    /// Compares two row versions as the big-endian unsigned integers SQL Server treats
    /// them as. A plain byte-array comparison would order them wrongly.
    /// </summary>
    public static int Compare(byte[]? left, byte[]? right)
    {
        var leftValue = ToUInt64(left);
        var rightValue = ToUInt64(right);

        return leftValue.CompareTo(rightValue);
    }

    private static ulong ToUInt64(byte[]? rowVersion) =>
        rowVersion is null || rowVersion.Length != RowVersionLength
            ? 0UL
            : BinaryPrimitives.ReadUInt64BigEndian(rowVersion);
}
