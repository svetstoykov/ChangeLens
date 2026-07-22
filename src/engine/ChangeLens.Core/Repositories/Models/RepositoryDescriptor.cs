namespace ChangeLens.Core.Repositories.Models;

/// <summary>
///     Represents the immutable identity and current HEAD state of a Git repository.
/// </summary>
/// <param name="Name">The repository display name. Cannot be <see langword="null" /> or empty.</param>
/// <param name="CanonicalPath">The canonical absolute worktree path. Cannot be <see langword="null" /> or empty.</param>
/// <param name="Head">The current repository HEAD. Cannot be <see langword="null" />.</param>
public sealed record RepositoryDescriptor(
    string Name,
    string CanonicalPath,
    RepositoryHead Head);
