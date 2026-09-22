using System.Text;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;

namespace ChangeLens.Engine.AnalysisRuns.Helpers;

/// <summary>
///     Decides whether a reading projection keeps the completed poll response inside the shell's response-line limit.
/// </summary>
internal static class ReadingProjectionBudget
{
    /// <summary>Determines whether the projection fits the completed poll response budget.</summary>
    /// <remarks>
    ///     The budget is the UTF-8 size of both documents plus <see cref="AnalysisRunLimits.PollSummaryMaxBytes" /> of
    ///     headroom for the rest of the summary, compared with <see cref="AnalysisRunLimits.MaximumPollResponseBytes" />.
    /// </remarks>
    /// <param name="projection">The projection to measure. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the completed poll carrying the projection stays within the limit.</returns>
    internal static bool Fits(AnalysisReadingProjection projection)
    {
        long bytes = Encoding.UTF8.GetByteCount(projection.ReadingModelJson);
        bytes += Encoding.UTF8.GetByteCount(projection.ValidationRemovalsJson);
        bytes += AnalysisRunLimits.PollSummaryMaxBytes;
        return bytes <= AnalysisRunLimits.MaximumPollResponseBytes;
    }
}
