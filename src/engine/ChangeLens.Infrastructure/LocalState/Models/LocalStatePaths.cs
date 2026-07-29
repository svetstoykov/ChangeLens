using ChangeLens.Infrastructure.LocalState.Constants;

namespace ChangeLens.Infrastructure.LocalState.Models;

/// <summary>
///     Represents the immutable filesystem paths used by local-state persistence.
/// </summary>
public sealed class LocalStatePaths
{
    private LocalStatePaths(string directoryPath)
    {
        this.DirectoryPath = directoryPath;
        this.DatabasePath = Path.Combine(directoryPath, LocalStateConstants.DatabaseFileName);
    }

    /// <summary>
    ///     Gets the full local-state directory path.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    ///     Gets the full SQLite database path.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    ///     Resolves local-state paths from the configured directory or the local application-data fallback.
    /// </summary>
    /// <param name="configuredDirectory">
    ///     The configured directory, or <see langword="null" /> or blank to use local application data.
    /// </param>
    /// <returns>The resolved immutable local-state paths.</returns>
    public static LocalStatePaths Resolve(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return new LocalStatePaths(Path.GetFullPath(configuredDirectory));
        }

        var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new LocalStatePaths(Path.Combine(localApplicationDataPath, LocalStateConstants.ProductName));
    }
}
