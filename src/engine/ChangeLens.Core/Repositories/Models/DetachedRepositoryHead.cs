namespace ChangeLens.Core.Repositories.Models;

/// <summary>
///     Represents a repository HEAD detached from a branch.
/// </summary>
/// <param name="Revision">
///     The full lowercase SHA-1 or SHA-256 object identifier. Cannot be <see langword="null" /> or empty.
/// </param>
public sealed record DetachedRepositoryHead(
    string Revision) : RepositoryHead(Revision);
