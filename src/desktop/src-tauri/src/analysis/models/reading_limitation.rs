use crate::analysis::models::ReadingLimitationKind;
use serde::{Deserialize, Serialize};

/// Represents a disclosed reading limitation.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingLimitation {
    pub kind: ReadingLimitationKind,
    pub path: Option<String>,
    pub detail: String,
}
