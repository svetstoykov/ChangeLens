using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents the parameters for opening a repository.
/// </summary>
internal sealed class RepositoryOpenParameters
{
    /// <summary>
    ///     Gets the selected repository directory path.
    /// </summary>
    [JsonRequired]
    public string Path { get; init; } = null!;
}
