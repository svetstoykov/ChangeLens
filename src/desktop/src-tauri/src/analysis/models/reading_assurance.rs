use crate::analysis::models::ReadingAssuranceKind;
use serde::{Deserialize, Serialize};

/// Represents a disclosed verification assurance gap in the reading model.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingAssurance {
    pub kind: ReadingAssuranceKind,
    pub detail: String,
    pub claim_id: Option<String>,
}
