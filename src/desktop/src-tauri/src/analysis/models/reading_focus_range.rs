use serde::{Deserialize, Serialize};

/// Represents one highlighted focus range within a citation.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingFocusRange {
    pub node_id: String,
    pub start_line: u32,
    pub end_line: u32,
}
