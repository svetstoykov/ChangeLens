using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.LocalState.Models;

/// <summary>
///     Represents the optional repository selected and revalidated for startup restoration.
/// </summary>
/// <param name="HistoryEntry">The retained history entry, or <see langword="null" /> when none is selected.</param>
/// <param name="Repository">The revalidated repository, or <see langword="null" /> when none is selected.</param>
public sealed record RepositoryRestoration(
    RepositoryHistoryEntry? HistoryEntry,
    RepositoryDescriptor? Repository);
