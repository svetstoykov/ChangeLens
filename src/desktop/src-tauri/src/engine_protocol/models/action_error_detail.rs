use crate::engine_protocol::OperationErrorType;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ActionErrorDetail {
    #[serde(rename = "type")]
    pub error_type: OperationErrorType,
    pub code: String,
    pub message: String,
}

impl ActionErrorDetail {
    pub(crate) fn new(
        error_type: OperationErrorType,
        code: impl Into<String>,
        message: impl Into<String>,
    ) -> Self {
        Self {
            error_type,
            code: code.into(),
            message: message.into(),
        }
    }

    pub(crate) fn is_valid(&self) -> bool {
        !self.code.trim().is_empty() && !self.message.trim().is_empty()
    }
}
