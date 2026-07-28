using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.Diagnostics.Services;

/// <summary>
///     Converts local filesystem paths into forms safe for <c>Information</c>-level logging.
/// </summary>
/// <remarks>
///     <para>
///         Stateless and safe to call concurrently.
///     </para>
///     <para>
///         The current user's home directory is resolved once per process. A change to the <c>HOME</c> or
///         <c>USERPROFILE</c> environment variable after startup is not observed.
///     </para>
/// </remarks>
public static class PathSanitizer
{
    private const string OutsideRepositoryPlaceholder = "<path outside repository>";
    private const string HomeDirectoryPlaceholder = "~";
    private const string CurrentDirectorySegment = ".";
    private const string ParentDirectorySegment = "..";

    private static readonly string HomeDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="absolutePath" /> or <paramref name="repository" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="absolutePath" /> is empty.</exception>
    public static string ToRepositoryRelativePath(string absolutePath, RepositoryDescriptor repository)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath);
        ArgumentNullException.ThrowIfNull(repository);

        if (!TryGetContainedRelativePath(repository.CanonicalPath, absolutePath, out var relativePath))
        {
            return OutsideRepositoryPlaceholder;
        }

        return relativePath.Length == 0
            ? repository.Name
            : $"{repository.Name}/{relativePath}";
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="absolutePath" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="absolutePath" /> is empty.</exception>
    public static string RedactHomeDirectory(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath);

        if (HomeDirectory.Length == 0 ||
            !TryGetContainedRelativePath(HomeDirectory, absolutePath, out var relativePath))
        {
            return absolutePath;
        }

        return relativePath.Length == 0
            ? HomeDirectoryPlaceholder
            : $"{HomeDirectoryPlaceholder}/{relativePath}";
    }

    /// <summary>
    ///     Expresses an absolute path as a forward-slash path relative to a containing root directory.
    /// </summary>
    /// <param name="rootPath">The containing root directory. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="absolutePath">The path to express relative to <paramref name="rootPath" />.</param>
    /// <param name="relativePath">
    ///     When this method returns <see langword="true" />, the relative path, or an empty string when
    ///     <paramref name="absolutePath" /> is <paramref name="rootPath" /> itself. Otherwise an empty string.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="absolutePath" /> lies at or below
    ///     <paramref name="rootPath" />; otherwise, <see langword="false" />.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         <see cref="Path.GetRelativePath" /> fully qualifies both arguments, so relative segments in
    ///         <paramref name="absolutePath" /> are resolved before containment is decided and cannot escape the
    ///         root undetected. It also applies the platform's own path comparison, which keeps prefix matching
    ///         case-sensitive on Unix and case-insensitive on Windows.
    ///     </para>
    /// </remarks>
    private static bool TryGetContainedRelativePath(string rootPath, string absolutePath, out string relativePath)
    {
        var candidate = Path.GetRelativePath(rootPath, absolutePath);

        if (candidate == CurrentDirectorySegment)
        {
            relativePath = string.Empty;
            return true;
        }

        if (Path.IsPathRooted(candidate) || IsParentTraversal(candidate))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = NormalizeSeparators(candidate);
        return true;
    }

    /// <summary>
    ///     Determines whether a relative path leads out of the root it was resolved against.
    /// </summary>
    /// <param name="relativePath">The relative path to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="relativePath" /> starts with a <c>..</c> segment;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         The leading <c>..</c> must be a whole segment. A file legitimately named <c>..config</c> stays
    ///         inside the root.
    ///     </para>
    /// </remarks>
    private static bool IsParentTraversal(string relativePath)
    {
        return relativePath.StartsWith(ParentDirectorySegment, StringComparison.Ordinal) &&
               (relativePath.Length == ParentDirectorySegment.Length ||
                IsDirectorySeparator(relativePath[ParentDirectorySegment.Length]));
    }

    private static bool IsDirectorySeparator(char value)
    {
        return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
    }

    /// <summary>
    ///     Rewrites platform directory separators as forward slashes so log output is stable across platforms.
    /// </summary>
    /// <param name="path">The path to rewrite.</param>
    /// <returns>The path using forward slashes.</returns>
    /// <remarks>
    ///     <para>
    ///         Only <see cref="Path.DirectorySeparatorChar" /> is replaced. It is already a forward slash on Unix,
    ///         where the replacement is a no-op that returns the same instance, and a backslash on Windows, whose
    ///         alternate separator is itself a forward slash. Replacing the alternate separator as well would
    ///         corrupt Unix file names, where a backslash is a legal character.
    ///     </para>
    /// </remarks>
    private static string NormalizeSeparators(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }
}
