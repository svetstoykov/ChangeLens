use serde::{Deserialize, Serialize};

/// Names the recommendation derived from the findings of a review that ran.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingReviewRecommendation {
    DefectsToFix,
    IssuesWorthAddressing,
    NoDefectsFound,
    NoDefectsConfirmed,
}
