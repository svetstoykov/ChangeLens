use crate::analysis::models::{ReadingReviewRecommendation, ReadingReviewStatus};
use serde::{Deserialize, Serialize};

/// Represents the published outcome of the review.
///
/// A review that ran carries a recommendation. Any other status carries none and withholds nothing.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(
    rename_all = "camelCase",
    deny_unknown_fields,
    try_from = "ReadingReviewWire"
)]
pub struct ReadingReview {
    pub status: ReadingReviewStatus,
    pub recommendation: Option<ReadingReviewRecommendation>,
    pub withheld_count: u32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct ReadingReviewWire {
    status: ReadingReviewStatus,
    recommendation: Option<ReadingReviewRecommendation>,
    withheld_count: u32,
}

impl TryFrom<ReadingReviewWire> for ReadingReview {
    type Error = &'static str;

    fn try_from(wire: ReadingReviewWire) -> Result<Self, Self::Error> {
        let ran = wire.status == ReadingReviewStatus::Ran;

        if ran != wire.recommendation.is_some() {
            return Err("a review carries a recommendation exactly when it ran");
        }

        if !ran && wire.withheld_count != 0 {
            return Err("a review that did not run withholds no findings");
        }

        Ok(Self {
            status: wire.status,
            recommendation: wire.recommendation,
            withheld_count: wire.withheld_count,
        })
    }
}
