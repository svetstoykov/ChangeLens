namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents earlier first-parent commits that changed both a changed file and a candidate file.
/// </summary>
/// <param name="ChangedPath">The current repository-relative path of the changed file. Cannot be <see langword="null" />.</param>
/// <param name="Contribution">The positive, capped, recency-weighted amount this signal added to the candidate score.</param>
/// <param name="CommitCount">The number of inspected history commits that changed both files.</param>
public sealed record CoChangeCorrespondenceSignal(string ChangedPath, double Contribution, int CommitCount)
    : CorrespondenceSignal(CorrespondenceSignalKind.CoChange, ChangedPath, Contribution);
