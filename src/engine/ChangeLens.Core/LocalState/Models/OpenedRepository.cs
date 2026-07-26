using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.LocalState.Models;

/// <summary>
///     Represents a successfully inspected and durably recorded repository.
/// </summary>
/// <param name="HistoryEntry">The retained repository-history entry.</param>
/// <param name="Repository">The current inspected repository facts.</param>
public sealed record OpenedRepository(
    RepositoryHistoryEntry HistoryEntry,
    RepositoryDescriptor Repository);
