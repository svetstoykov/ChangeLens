namespace ChangeLens.Core.Repositories.Models;

/// <summary>
///     Represents a repository HEAD attached to a branch.
/// </summary>
/// <param name="Name">The short branch name. Cannot be <see langword="null" /> or whitespace.</param>
/// <param name="Revision">
///     The full lowercase SHA-1 or SHA-256 object identifier. Cannot be <see langword="null" /> or empty.
/// </param>
public sealed record BranchRepositoryHead(
    string Name,
    string Revision) : RepositoryHead(Revision);
