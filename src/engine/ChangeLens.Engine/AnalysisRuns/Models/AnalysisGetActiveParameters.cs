using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the parameters for looking up the active analysis run for a repository.
/// </summary>
internal sealed class AnalysisGetActiveParameters
{
    /// <summary>Gets the selected repository directory path.</summary>
    /// <value>The repository directory path. It cannot be <see langword="null" />.</value>
    [JsonRequired]
    public string Path { get; init; } = null!;
}
