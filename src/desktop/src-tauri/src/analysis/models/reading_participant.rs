use serde::{Deserialize, Serialize};

/// Represents a participant in a reading area.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingParticipant {
    pub id: String,
    pub name: String,
    pub role: String,
    pub changed: bool,
    pub evidence_node_ids: Vec<String>,
}
