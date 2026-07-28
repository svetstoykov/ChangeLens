using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.Diagnostics.Interfaces;

/// <summary>
///     Defines conversions from local filesystem paths to forms safe for <c>Information</c>-level logging.
/// </summary>
/// <remarks>
///     <para>
///         Implementations are registered as singletons and must be safe for concurrent calls.
///     </para>
/// </remarks>
public interface IPathSanitizer
{
    /// <summary>
    ///     Converts an absolute path known to live under a repository into that repository's display name
    ///     plus the path relative to its canonical root.
    /// </summary>
    /// <param name="absolutePath">
    ///     The absolute path to convert. Cannot be <see langword="null" /> or empty.
    /// </param>
    /// <param name="repository">The repository the path is expected to live under. Cannot be <see langword="null" />.</param>
    /// <returns>
    ///     The repository display name, or the display name followed by the relative path when
    ///     <paramref name="absolutePath" /> is not the repository root itself. A fixed placeholder when
    ///     <paramref name="absolutePath" /> does not fall under <paramref name="repository" />'s canonical root.
    /// </returns>
    string ToRepositoryRelativePath(string absolutePath, RepositoryDescriptor repository);

    /// <summary>
    ///     Redacts the current user's home-directory segment from an absolute path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to redact. Cannot be <see langword="null" /> or empty.</param>
    /// <returns>
    ///     The path with its home-directory prefix replaced by <c>~</c>, or <paramref name="absolutePath" />
    ///     unchanged when it does not fall under the current user's home directory.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         A path outside the home directory is returned in full. Callers that must never emit a raw local
    ///         path cannot rely on this method alone.
    ///     </para>
    /// </remarks>
    string RedactHomeDirectory(string absolutePath);
}
