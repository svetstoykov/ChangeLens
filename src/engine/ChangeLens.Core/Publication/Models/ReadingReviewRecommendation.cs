namespace ChangeLens.Core.Publication.Models;

/// <summary>Defines the recommendation derived from the published findings of a review that ran.</summary>
public enum ReadingReviewRecommendation
{
    /// <summary>At least one Critical finding is published.</summary>
    DefectsToFix,

    /// <summary>At least one Warning and no Critical finding is published.</summary>
    IssuesWorthAddressing,

    /// <summary>Nothing at Critical or Warning is published and validation withheld nothing.</summary>
    NoDefectsFound,

    /// <summary>Nothing at Critical or Warning is published and validation withheld at least one finding.</summary>
    NoDefectsConfirmed,
}
