using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the tagged analysis-start outcome union in the engine protocol.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "state")]
[JsonDerivedType(typeof(AcceptedAnalysisStartResult), "accepted")]
[JsonDerivedType(typeof(RejectedStaleAnalysisStartResult), "rejectedStale")]
[JsonDerivedType(typeof(RejectedActiveAnalysisStartResult), "rejectedActive")]
internal abstract record AnalysisStartResult;
