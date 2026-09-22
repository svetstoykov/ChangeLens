use crate::analysis::models::ReadingTrust;
use serde::{Deserialize, Serialize};

/// Represents a claim statement within the reading model.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingStatement {
    pub claim_id: String,
    pub text: String,
    pub trust: ReadingTrust,
    pub evidence_node_ids: Vec<String>,
}
