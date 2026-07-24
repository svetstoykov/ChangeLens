using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the parameters for preparing a comparison.
/// </summary>
internal sealed class ComparisonPrepareParameters
{
    /// <summary>
    ///     Gets the selected repository directory path.
    /// </summary>
    [JsonRequired]
    public string Path { get; init; } = null!;

    /// <summary>
    ///     Gets the exact full local or cached remote-tracking reference.
    /// </summary>
    [JsonRequired]
    public string Target { get; init; } = null!;
}
