using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the explicit deterministic check selection for a start request.
/// </summary>
internal sealed class AnalysisCheckSelectionParameters
{
    /// <summary>Gets a value indicating whether the build check is selected.</summary>
    /// <value><see langword="true" /> if the build check is selected; otherwise, <see langword="false" />.</value>
    [JsonRequired]
    public bool Build { get; init; }

    /// <summary>Gets a value indicating whether the test check is selected.</summary>
    /// <value><see langword="true" /> if the test check is selected; otherwise, <see langword="false" />.</value>
    [JsonRequired]
    public bool Tests { get; init; }
}
