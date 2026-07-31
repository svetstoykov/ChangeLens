using System.Collections.Frozen;
using ChangeLens.Core.AnalysisRuns.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChangeLens.Infrastructure.AnalysisRuns.Persistence.Converters;

/// <summary>
///     Converts <see cref="AnalysisRunStepState" /> to and from the camelCase literal enforced by the
///     <c>CK_analysis_run_steps_state</c> check constraint.
/// </summary>
internal sealed class AnalysisRunStepStateValueConverter() : ValueConverter<AnalysisRunStepState, string>(
    state => ToLiteral(state),
    literal => FromLiteral(literal))
{
    private static readonly FrozenDictionary<AnalysisRunStepState, string> LiteralsByState =
        new Dictionary<AnalysisRunStepState, string>
        {
            [AnalysisRunStepState.Pending] = "pending",
            [AnalysisRunStepState.Running] = "running",
            [AnalysisRunStepState.Succeeded] = "succeeded",
            [AnalysisRunStepState.SucceededWithLimitations] = "succeededWithLimitations",
            [AnalysisRunStepState.Failed] = "failed",
            [AnalysisRunStepState.Skipped] = "skipped",
            [AnalysisRunStepState.Cancelled] = "cancelled",
            [AnalysisRunStepState.TimedOut] = "timedOut",
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, AnalysisRunStepState> StatesByLiteral =
        LiteralsByState.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    private static string ToLiteral(AnalysisRunStepState state) => LiteralsByState[state];

    private static AnalysisRunStepState FromLiteral(string literal) => StatesByLiteral[literal];
}
