using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents repository identity and HEAD state in the engine protocol.
/// </summary>
/// <param name="Name">The repository display name.</param>
/// <param name="CanonicalPath">The canonical absolute worktree path.</param>
/// <param name="Head">The current committed repository HEAD.</param>
internal sealed record RepositoryResult(
    string Name,
    string CanonicalPath,
    RepositoryHeadResult Head)
{
    /// <summary>
    ///     Maps a Core repository descriptor to its versioned protocol result.
    /// </summary>
    /// <param name="descriptor">The repository descriptor to map. Cannot be <see langword="null" />.</param>
    /// <returns>The repository protocol result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="descriptor" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The descriptor contains an unapproved repository HEAD subtype.
    /// </exception>
    internal static RepositoryResult FromDescriptor(RepositoryDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        RepositoryHeadResult head = descriptor.Head switch
        {
            BranchRepositoryHead branch => new BranchRepositoryHeadResult(
                branch.Name,
                branch.Revision),
            DetachedRepositoryHead detached => new DetachedRepositoryHeadResult(
                detached.Revision),
            _ => throw new InvalidOperationException(
                "The repository HEAD type is not approved for the engine protocol."),
        };

        return new RepositoryResult(
            descriptor.Name,
            descriptor.CanonicalPath,
            head);
    }
}
