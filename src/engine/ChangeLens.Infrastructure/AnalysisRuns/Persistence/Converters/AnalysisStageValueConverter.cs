using System.Collections.Frozen;
using ChangeLens.Core.AnalysisRuns.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChangeLens.Infrastructure.AnalysisRuns.Persistence.Converters;

/// <summary>
///     Converts <see cref="AnalysisStage" /> to and from its camelCase stored literal, consistent with the
///     camelCase state literals enforced by the analysis run and analysis run step check constraints.
/// </summary>
internal sealed class AnalysisStageValueConverter() : ValueConverter<AnalysisStage, string>(
    stage => ToLiteral(stage),
    literal => FromLiteral(literal))
{
    private static readonly FrozenDictionary<AnalysisStage, string> LiteralsByStage = new Dictionary<AnalysisStage, string>
    {
        [AnalysisStage.Capturing] = "capturing",
        [AnalysisStage.Discovering] = "discovering",
        [AnalysisStage.Collecting] = "collecting",
        [AnalysisStage.Persisting] = "persisting",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, AnalysisStage> StagesByLiteral =
        LiteralsByStage.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    private static string ToLiteral(AnalysisStage stage) => LiteralsByStage[stage];

    private static AnalysisStage FromLiteral(string literal) => StagesByLiteral[literal];
}
