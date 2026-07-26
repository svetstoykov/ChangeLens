using ChangeLens.Core.LocalState.Interfaces;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Provides platform-specific canonical repository path identity.
/// </summary>
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
