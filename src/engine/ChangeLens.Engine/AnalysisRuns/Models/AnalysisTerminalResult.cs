using System.Text.Json.Serialization;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the strict terminal outcome union in the engine protocol.
/// </summary>
/// <param name="TerminalAt">The Unix timestamp in milliseconds when the run became terminal.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CompletedAnalysisTerminalResult), "completed")]
[JsonDerivedType(typeof(CompletedWithLimitationsAnalysisTerminalResult), "completedWithLimitations")]
[JsonDerivedType(typeof(CancelledAnalysisTerminalResult), "cancelled")]
[JsonDerivedType(typeof(FailedAnalysisTerminalResult), "failed")]
internal abstract record AnalysisTerminalResult(long TerminalAt);
