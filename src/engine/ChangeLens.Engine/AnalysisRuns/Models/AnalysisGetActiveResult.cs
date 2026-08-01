using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the tagged active-run lookup union in the engine protocol.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "state")]
[JsonDerivedType(typeof(NoneAnalysisGetActiveResult), "none")]
[JsonDerivedType(typeof(ActiveAnalysisGetActiveResult), "active")]
internal abstract record AnalysisGetActiveResult;
