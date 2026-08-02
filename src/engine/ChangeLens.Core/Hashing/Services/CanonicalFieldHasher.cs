using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ChangeLens.Core.Hashing.Services;

/// <summary>
///     Provides the canonical length-prefixed UTF-8 field encoding used by every ChangeLens SHA-256 fingerprint.
/// </summary>
internal static class CanonicalFieldHasher
{
    /// <summary>Rejects unpaired UTF-16 surrogates instead of replacing them in fingerprint fields.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Appends one UTF-8 field after its four-byte big-endian byte length.</summary>
    /// <param name="hash">The incremental hash that receives the canonical field. Cannot be <see langword="null" />.</param>
    /// <param name="value">The field value to encode. Cannot be <see langword="null" />.</param>
    internal static void AppendField(IncrementalHash hash, string value)
    {
        var bytes = StrictUtf8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    /// <summary>Appends a marker followed by an explicit absent or present value.</summary>
    /// <param name="hash">The incremental hash. Cannot be <see langword="null" />.</param>
    /// <param name="marker">The field marker. Cannot be <see langword="null" />.</param>
    /// <param name="value">The optional field value.</param>
    internal static void AppendNullableField(IncrementalHash hash, string marker, string? value)
    {
        AppendField(hash, marker);

        if (value is null)
        {
            AppendField(hash, "absent");
            return;
        }

        AppendField(hash, "present");
        AppendField(hash, value);
    }

    /// <summary>Appends a marker followed by a stable Boolean literal.</summary>
    /// <param name="hash">The incremental hash. Cannot be <see langword="null" />.</param>
    /// <param name="marker">The field marker. Cannot be <see langword="null" />.</param>
    /// <param name="value">The Boolean value.</param>
    internal static void AppendBooleanField(IncrementalHash hash, string marker, bool value)
    {
        AppendField(hash, marker);
        AppendField(hash, value ? "true" : "false");
    }

    /// <summary>Completes the hash and renders it as lowercase hexadecimal.</summary>
    /// <param name="hash">The incremental hash. Cannot be <see langword="null" />.</param>
    /// <returns>A deterministic 64-character lowercase SHA-256 value.</returns>
    internal static string Complete(IncrementalHash hash) =>
        Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
}
