use crate::analysis::models::ReadingOmissionSourceKind;
use serde::{Deserialize, Serialize};

/// Represents a bounded summary of evidence omitted from the reading model.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingOmissionSummary {
    pub source_kind: ReadingOmissionSourceKind,
    pub reason: String,
    pub total_count: u32,
    pub sample_count: u32,
    pub resolved_sample_count: u32,
}
