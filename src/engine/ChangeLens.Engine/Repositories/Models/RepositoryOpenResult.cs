using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents the result of opening a repository.
/// </summary>
/// <param name="Repository">The inspected repository identity and HEAD state.</param>
internal sealed record RepositoryOpenResult(RepositoryResult Repository)
{
    /// <summary>
    ///     Maps a Core repository descriptor to its versioned protocol result.
    /// </summary>
    /// <param name="descriptor">The repository descriptor to map. Cannot be <see langword="null" />.</param>
    /// <returns>The repository-open protocol result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="descriptor" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The descriptor contains an unapproved repository HEAD subtype.
    /// </exception>
    internal static RepositoryOpenResult FromDescriptor(RepositoryDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new RepositoryOpenResult(RepositoryResult.FromDescriptor(descriptor));
    }
}
