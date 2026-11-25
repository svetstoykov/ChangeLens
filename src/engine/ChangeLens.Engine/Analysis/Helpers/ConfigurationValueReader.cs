using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ChangeLens.Engine.Analysis.Helpers;

/// <summary>
///     Provides configuration-value parsing helpers with explicit fallback semantics.
/// </summary>
internal static class ConfigurationValueReader
{
    /// <summary>Reads an integer configuration value, preserving valid non-positive numbers for startup validation.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent, empty, or not a valid integer.</param>
    /// <returns>The parsed integer value or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static int ReadInt(IConfiguration configuration, string key, int fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return int.TryParse(configuration[key], out var value) ? value : fallback;
    }

    /// <summary>Reads one positive integer configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The positive configured value or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static int ReadPositiveInt(IConfiguration configuration, string key, int fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
    }

    /// <summary>Reads a non-negative integer configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured value or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static int ReadNonNegativeInt(IConfiguration configuration, string key, int fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return int.TryParse(configuration[key], out var value) && value >= 0 ? value : fallback;
    }

    /// <summary>Reads an optional positive integer configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <returns>The configured value, or <see langword="null" /> when absent or invalid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static int? ReadOptionalPositiveInt(IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return int.TryParse(configuration[key], out var value) && value > 0 ? value : null;
    }

    /// <summary>Reads a fractional configuration value in the half-open range greater than zero up to one.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured fraction or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static double ReadFraction(IConfiguration configuration, string key, double fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 && value <= 1
            ? value
            : fallback;
    }

    /// <summary>Reads one positive duration configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The positive configured duration or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static TimeSpan ReadPositiveTimeSpan(IConfiguration configuration, string key, TimeSpan fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return TimeSpan.TryParse(configuration[key], out var value) && value > TimeSpan.Zero ? value : fallback;
    }

    /// <summary>Reads one Boolean configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured value or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static bool ReadBoolean(IConfiguration configuration, string key, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return bool.TryParse(configuration[key], out var value) ? value : fallback;
    }

    /// <summary>Reads one case-insensitive optional enumeration configuration value.</summary>
    /// <typeparam name="TEnum">The enumeration type to parse.</typeparam>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured enumeration value or <paramref name="fallback" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="key" /> is <see langword="null" />.</exception>
    internal static TEnum? ReadEnum<TEnum>(IConfiguration configuration, string key, TEnum? fallback)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(key);

        return Enum.TryParse<TEnum>(configuration[key], true, out var value) ? value : fallback;
    }
}
