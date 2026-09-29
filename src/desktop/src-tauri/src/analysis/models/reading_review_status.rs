use serde::{Deserialize, Serialize};

/// Describes what happened to the review that produced the published findings.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingReviewStatus {
    Ran,
    NotRun,
    Failed,
    TooLarge,
}
