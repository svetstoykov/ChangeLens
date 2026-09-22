use crate::analysis::models::{
    ReadingArea, ReadingAssurance, ReadingCitation, ReadingEvidence, ReadingLimitation,
    ReadingOmissionSummary, ReadingStatement,
};
use serde::{Deserialize, Serialize};

/// Represents the complete reading model published with an analysis run.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingModel {
    pub thesis: Option<ReadingStatement>,
    pub areas: Vec<ReadingArea>,
    pub citations: Vec<ReadingCitation>,
    pub evidence: Vec<ReadingEvidence>,
    pub limitations: Vec<ReadingLimitation>,
    pub omission_summaries: Vec<ReadingOmissionSummary>,
    pub assurances: Vec<ReadingAssurance>,
}
