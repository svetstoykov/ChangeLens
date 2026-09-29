namespace ChangeLens.Core.Publication.Models;

/// <summary>Defines what happened to the review that produced the published findings.</summary>
public enum ReadingReviewStatus
{
    /// <summary>The review ran and its validated findings are published.</summary>
    Ran,

    /// <summary>The review was not run.</summary>
    NotRun,

    /// <summary>The review failed or returned an unreadable reply.</summary>
    Failed,

    /// <summary>The review's contribution was dropped because it did not fit the response budget.</summary>
    TooLarge,
}
