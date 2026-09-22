use crate::analysis::models::ReadingSide;
use serde::{Deserialize, Serialize};

/// Represents a single disclosed evidence excerpt in the reading model.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingEvidence {
    pub node_id: String,
    pub path: String,
    pub side: ReadingSide,
    pub start_line: u32,
    pub end_line: u32,
    pub is_changed_file: bool,
    pub is_redacted: bool,
    pub is_truncated: bool,
    pub text: String,
}
