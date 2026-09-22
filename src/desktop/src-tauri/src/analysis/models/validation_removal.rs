use crate::analysis::models::ValidationRemovalScope;
use serde::{Deserialize, Serialize};

/// Represents a validation removal recorded against a reading entity.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ValidationRemoval {
    pub scope: ValidationRemovalScope,
    pub id: String,
    pub reason: String,
}
