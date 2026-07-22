namespace ChangeLens.Core.Repositories.Models;

/// <summary>
///     Represents the current committed HEAD of a repository.
/// </summary>
/// <param name="Revision">
///     The full lowercase SHA-1 or SHA-256 object identifier. Cannot be <see langword="null" /> or empty.
/// </param>
public abstract record RepositoryHead(string Revision);
