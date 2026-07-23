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
    RepositoryHeadResult Head);
