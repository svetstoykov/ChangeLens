use serde::{Deserialize, Serialize};

/// Identifies the reading entity a validation removal applies to.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ValidationRemovalScope {
    Track,
    Participant,
    Relationship,
    Statement,
}
