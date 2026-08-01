using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the parameters for starting an analysis run.
/// </summary>
internal sealed class AnalysisStartParameters
{
    /// <summary>Gets the selected repository directory path.</summary>
    /// <value>The repository directory path. It cannot be <see langword="null" />.</value>
    [JsonRequired]
    public string Path { get; init; } = null!;

    /// <summary>Gets the exact local or cached remote-tracking reference.</summary>
    /// <value>The comparison target reference. It cannot be <see langword="null" />.</value>
    [JsonRequired]
    public string Target { get; init; } = null!;

    /// <summary>Gets the lowercase SHA-256 freshness token from comparison preparation.</summary>
    /// <value>The freshness token. It cannot be <see langword="null" />.</value>
    [JsonRequired]
    public string FreshnessToken { get; init; } = null!;

    /// <summary>Gets the optional developer-supplied change context.</summary>
    /// <value>The developer-supplied context, or <see langword="null" /> when none was provided.</value>
    public string? ChangeContext { get; init; }
}
