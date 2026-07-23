namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents a repository HEAD attached to a branch in the engine protocol.
/// </summary>
/// <param name="Name">The short branch name.</param>
/// <param name="Revision">The full lowercase Git object identifier.</param>
internal sealed record BranchRepositoryHeadResult(
    string Name,
    string Revision) : RepositoryHeadResult(Revision);
