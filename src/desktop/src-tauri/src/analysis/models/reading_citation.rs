use crate::analysis::models::{ReadingFocusRange, ReadingSide, ReadingTrust};
use serde::{Deserialize, Serialize};

/// Represents a citation that anchors a claim to a source location.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingCitation {
    pub claim_id: String,
    pub node_id: String,
    pub side: ReadingSide,
    pub path: String,
    pub object_id: String,
    pub start_line: u32,
    pub end_line: u32,
    pub focus: Vec<ReadingFocusRange>,
    pub provenance: ReadingTrust,
}
