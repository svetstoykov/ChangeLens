using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the parameters for refreshing a cached remote-tracking comparison baseline.
/// </summary>
internal sealed class ComparisonRefreshRemoteBaselineParameters
{
    /// <summary>
    ///     Gets the selected repository directory path.
    /// </summary>
    [JsonRequired]
    public string Path { get; init; } = null!;

    /// <summary>
    ///     Gets the exact full cached remote-tracking reference.
    /// </summary>
    [JsonRequired]
    public string Target { get; init; } = null!;
}
