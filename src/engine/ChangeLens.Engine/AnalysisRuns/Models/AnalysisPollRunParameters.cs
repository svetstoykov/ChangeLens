using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the parameters for polling one analysis run.
/// </summary>
internal sealed class AnalysisPollRunParameters
{
    /// <summary>Gets the run identifier supplied by the caller.</summary>
    /// <value>The run identifier. It must be a valid GUID string.</value>
    [JsonRequired]
    public string RunId { get; init; } = null!;
}
