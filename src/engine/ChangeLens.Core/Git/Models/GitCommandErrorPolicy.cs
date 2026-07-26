using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Defines the capability-selected errors returned when a bounded Git command cannot complete.
/// </summary>
public sealed class GitCommandErrorPolicy
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GitCommandErrorPolicy" /> class.
    /// </summary>
    /// <param name="timedOut">The error returned when the command exceeds its deadline. Cannot be <see langword="null" />.</param>
    /// <param name="outputLimitExceeded">The error returned when either captured stream exceeds its bound. Cannot be <see langword="null" />.</param>
    /// <param name="inspectionFailed">The error returned when the process output cannot be inspected. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="timedOut" />, <paramref name="outputLimitExceeded" />, or <paramref name="inspectionFailed" />
    ///     is <see langword="null" />.
    /// </exception>
    public GitCommandErrorPolicy(
        OperationError timedOut,
        OperationError outputLimitExceeded,
        OperationError inspectionFailed)
    {
        ArgumentNullException.ThrowIfNull(timedOut);
        ArgumentNullException.ThrowIfNull(outputLimitExceeded);
        ArgumentNullException.ThrowIfNull(inspectionFailed);
        this.TimedOut = timedOut;
        this.OutputLimitExceeded = outputLimitExceeded;
        this.InspectionFailed = inspectionFailed;
    }

    /// <summary>
    ///     Gets the error returned when the command exceeds its deadline.
    /// </summary>
    public OperationError TimedOut { get; }

    /// <summary>
    ///     Gets the error returned when either captured stream exceeds its bound.
    /// </summary>
    public OperationError OutputLimitExceeded { get; }

    /// <summary>
    ///     Gets the error returned when the process output cannot be inspected.
    /// </summary>
    public OperationError InspectionFailed { get; }
}
