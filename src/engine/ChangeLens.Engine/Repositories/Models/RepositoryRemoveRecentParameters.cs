using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents parameters for removing one repository-history entry.
/// </summary>
internal sealed class RepositoryRemoveRecentParameters
{
    /// <summary>
    ///     Gets the lowercase hyphenated ChangeLens repository identifier.
    /// </summary>
    [JsonRequired]
    public string RepositoryId { get; init; } = null!;
}
