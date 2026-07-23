namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents a repository HEAD detached from a branch in the engine protocol.
/// </summary>
/// <param name="Revision">The full lowercase Git object identifier.</param>
internal sealed record DetachedRepositoryHeadResult(
    string Revision) : RepositoryHeadResult(Revision);
