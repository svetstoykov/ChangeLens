using ChangeLens.Core.LocalState.Interfaces;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Provides platform-specific canonical repository path identity.
/// </summary>
/// <remarks>
///     The Engine registers this implementation as scoped. It serves one request and does not need to be thread-safe.
/// </remarks>
public sealed class CanonicalRepositoryPathKeyProvider : ICanonicalRepositoryPathKeyProvider
{
    /// <inheritdoc />
    public string CreateKey(string canonicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);

        return OperatingSystem.IsWindows()
            ? canonicalPath.ToUpperInvariant()
            : canonicalPath;
    }
}
