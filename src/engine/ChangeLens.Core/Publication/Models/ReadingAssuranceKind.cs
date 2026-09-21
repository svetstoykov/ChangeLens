namespace ChangeLens.Core.Publication.Models;

/// <summary>Defines a published assurance category.</summary>
public enum ReadingAssuranceKind
{
    /// <summary>The checker was not run.</summary>
    CheckerNotRun,

    /// <summary>The checker failed.</summary>
    CheckerFailed,

    /// <summary>Tests were not executed.</summary>
    TestsNotExecuted,

    /// <summary>The build was not executed.</summary>
    BuildNotExecuted,

    /// <summary>The repository was not fully read.</summary>
    RepositoryNotFullyRead,

    /// <summary>A published claim has no citation.</summary>
    ClaimNotCited,

    /// <summary>More than one published claimant used the same claim identifier.</summary>
    DuplicateClaimId,
}
