use crate::analysis::models::ReadingTrust;
use serde::{Deserialize, Serialize};

/// Represents a directed relationship between two participants in a reading area.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingRelationship {
    pub claim_id: String,
    pub id: String,
    pub from_participant_id: String,
    pub to_participant_id: String,
    pub kind: String,
    pub explanation: String,
    pub trust: ReadingTrust,
    pub evidence_node_ids: Vec<String>,
}
