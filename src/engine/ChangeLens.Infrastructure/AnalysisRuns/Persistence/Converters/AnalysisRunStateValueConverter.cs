using System.Collections.Frozen;
using ChangeLens.Core.AnalysisRuns.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChangeLens.Infrastructure.AnalysisRuns.Persistence.Converters;

/// <summary>
///     Converts <see cref="AnalysisRunState" /> to and from the camelCase literal enforced by the
///     <c>CK_analysis_runs_state</c> check constraint.
/// </summary>
internal sealed class AnalysisRunStateValueConverter() : ValueConverter<AnalysisRunState, string>(
    state => ToLiteral(state),
    literal => FromLiteral(literal))
{
    private static readonly FrozenDictionary<AnalysisRunState, string> LiteralsByState =
        new Dictionary<AnalysisRunState, string>
        {
            [AnalysisRunState.PendingCapture] = "pendingCapture",
            [AnalysisRunState.Capturing] = "capturing",
            [AnalysisRunState.Discovering] = "discovering",
            [AnalysisRunState.Collecting] = "collecting",
            [AnalysisRunState.Persisting] = "persisting",
            [AnalysisRunState.Completed] = "completed",
            [AnalysisRunState.CompletedWithLimitations] = "completedWithLimitations",
            [AnalysisRunState.Cancelled] = "cancelled",
            [AnalysisRunState.Failed] = "failed",
            [AnalysisRunState.Interrupted] = "interrupted",
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, AnalysisRunState> StatesByLiteral =
        LiteralsByState.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    private static string ToLiteral(AnalysisRunState state) => LiteralsByState[state];

    private static AnalysisRunState FromLiteral(string literal) => StatesByLiteral[literal];
}
