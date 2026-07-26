namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines platform-specific canonical worktree path identity.
/// </summary>
public interface ICanonicalRepositoryPathKeyProvider
{
    /// <summary>
    ///     Creates the internal comparison key for a canonical worktree path.
    /// </summary>
    /// <param name="canonicalPath">The canonical absolute worktree path.</param>
    /// <returns>The normalized comparison key.</returns>
    string CreateKey(string canonicalPath);
}
