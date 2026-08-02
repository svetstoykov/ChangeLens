using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Maps transport-independent analysis run details to their engine protocol representation.
/// </summary>
internal static class AnalysisProjectionMapper
{
    /// <summary>Maps one Core detail to its protocol representation.</summary>
    /// <param name="projection">The analysis run detail to map. Cannot be <see langword="null" />.</param>
    /// <returns>The protocol projection, or an error when a Core value has no approved protocol representation.</returns>
    internal static Result<Models.AnalysisRunProjectionResult> ToProtocol(AnalysisRunDetail projection)
    {
        var stateResult = ToStateString(projection.State);
        if (stateResult.IsFailure)
        {
            return Result.ErrorFromResult<Models.AnalysisRunProjectionResult>(stateResult);
        }

        var terminalResult = projection.Terminal is null
            ? Result.Success<Models.AnalysisTerminalResult?>(null)
            : ToTerminal(projection.Terminal);
        if (terminalResult.IsFailure)
        {
            return Result.ErrorFromResult<Models.AnalysisRunProjectionResult>(terminalResult);
        }

        return new Models.AnalysisRunProjectionResult(
            projection.RunId.ToString(),
            stateResult.Data!,
            new Models.AnalysisRepositoryResult(
                projection.Repository.RepositoryId.ToString(),
                projection.Repository.DisplayName,
                projection.Repository.CanonicalPath,
                projection.Repository.HeadRevision),
            new Models.AnalysisComparisonResult(
                projection.Comparison.Target,
                projection.Comparison.TargetRevision,
                projection.Comparison.FreshnessToken),
            projection.RequestedAtUnixMilliseconds,
            projection.CaptureStartedAtUnixMilliseconds,
            projection.CapturedAtUnixMilliseconds,
            projection.SnapshotId?.ToString(),
            projection.CancellationRequested,
            BuildFacts(projection),
            terminalResult.Data,
            projection.InterruptedAtUnixMilliseconds,
            projection.InterruptionReason);
    }

    private static IReadOnlyList<Models.AnalysisFactResult> BuildFacts(AnalysisRunDetail projection)
    {
        if (projection.CapturedAtUnixMilliseconds is null || projection.CapturedChangedFileCount is null)
        {
            return [];
        }

        var facts = new List<Models.AnalysisFactResult>(2)
        {
            new(AnalysisFactKind.ChangedFilesCaptured, projection.CapturedChangedFileCount.Value, null),
        };

        var counts = projection.ExcludedUncommittedCounts;
        if (counts is not null && counts.Total > 0)
        {
            facts.Add(new Models.AnalysisFactResult(AnalysisFactKind.ExcludedUncommittedFiles, counts.Total, DescribeExclusions(counts)));
        }

        return facts;
    }

    private static string DescribeExclusions(ExcludedUncommittedCounts counts)
    {
        var clauses = new List<string>(4);
        if (counts.Staged > 0)
        {
            clauses.Add($"{counts.Staged} staged");
        }

        if (counts.Unstaged > 0)
        {
            clauses.Add($"{counts.Unstaged} unstaged");
        }

        if (counts.Untracked > 0)
        {
            clauses.Add($"{counts.Untracked} untracked");
        }

        if (counts.Conflicted > 0)
        {
            clauses.Add($"{counts.Conflicted} conflicted");
        }

        return string.Join(", ", clauses);
    }

    private static Result<string> ToStateString(AnalysisRunState state) => state switch
    {
        AnalysisRunState.PendingCapture => "pendingCapture",
        AnalysisRunState.Capturing => "capturing",
        AnalysisRunState.Discovering => "discovering",
        AnalysisRunState.Collecting => "collecting",
        AnalysisRunState.Persisting => "persisting",
        AnalysisRunState.Completed => "completed",
        AnalysisRunState.CompletedWithLimitations => "completedWithLimitations",
        AnalysisRunState.Cancelled => "cancelled",
        AnalysisRunState.Failed => "failed",
        AnalysisRunState.Interrupted => "interrupted",
        _ => OperationError.InternalError("The analysis run state is not approved for the engine protocol.",
            AnalysisProtocolErrorCode.UnmappedRunState),
    };

    private static Result<Models.AnalysisTerminalResult?> ToTerminal(AnalysisTerminalSummary terminal) => terminal.Kind switch
    {
        AnalysisTerminalKind.Completed => new Models.CompletedAnalysisTerminalResult(terminal.TerminalAtUnixMilliseconds),
        AnalysisTerminalKind.CompletedWithLimitations => new Models.CompletedWithLimitationsAnalysisTerminalResult(
            terminal.TerminalAtUnixMilliseconds,
            terminal.LimitationCount!.Value),
        AnalysisTerminalKind.Cancelled => new Models.CancelledAnalysisTerminalResult(terminal.TerminalAtUnixMilliseconds),
        AnalysisTerminalKind.Failed => new Models.FailedAnalysisTerminalResult(terminal.TerminalAtUnixMilliseconds, terminal.FailureCode!),
        _ => OperationError.InternalError("The terminal kind is not approved for the engine protocol.",
            AnalysisProtocolErrorCode.UnmappedTerminalKind),
    };
}
