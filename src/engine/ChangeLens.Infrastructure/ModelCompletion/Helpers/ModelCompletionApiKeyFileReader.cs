using ChangeLens.Infrastructure.ModelCompletion.Constants;

namespace ChangeLens.Infrastructure.ModelCompletion.Helpers;

/// <summary>
///     Reads the provider API key from a local key file.
/// </summary>
/// <remarks>
///     The key file holds shell-style <c>NAME=value</c> lines, each optionally prefixed with <c>export</c>. The reader takes the
///     value of the last <see cref="ModelCompletionConfigurationConstants.ApiKeyEntryName" /> line, as sourcing the file would,
///     and strips one pair of matching surrounding quotes. It is stateless and thread-safe.
/// </remarks>
public static class ModelCompletionApiKeyFileReader
{
    private const string ExportPrefix = "export ";
    private const string HomePrefix = "~/";

    /// <summary>
    ///     Reads the API key from the key file at a path, where a leading <c>~/</c> stands for the user's home directory.
    /// </summary>
    /// <param name="path">The key file path, or <see langword="null" /> when none is configured.</param>
    /// <returns>
    ///     The API key, or <see langword="null" /> when no path is configured, the file is missing or unreadable, or it has no
    ///     non-empty key entry.
    /// </returns>
    public static string? Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var fullPath = path.StartsWith(HomePrefix, StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[HomePrefix.Length..])
            : path;
        try
        {
            return File.Exists(fullPath) ? File.ReadLines(fullPath).Select(EntryValue).LastOrDefault(value => value is not null) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? EntryValue(string line)
    {
        var entry = line.Trim();
        if (entry.StartsWith(ExportPrefix, StringComparison.Ordinal))
            entry = entry[ExportPrefix.Length..].TrimStart();

        var separator = entry.IndexOf('=', StringComparison.Ordinal);
        if (separator < 0 || entry[..separator].Trim() != ModelCompletionConfigurationConstants.ApiKeyEntryName)
            return null;

        var value = entry[(separator + 1)..].Trim();
        if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
            value = value[1..^1];
        return value.Length == 0 ? null : value;
    }
}
