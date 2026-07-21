use crate::engine_protocol::{ActionErrorDetail, ActionErrorKind, OperationErrorType};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct EngineActionError {
    pub kind: ActionErrorKind,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub request_id: Option<String>,
    pub errors: Vec<ActionErrorDetail>,
}

impl EngineActionError {
    pub(crate) fn operation(request_id: String, errors: Vec<ActionErrorDetail>) -> Self {
        Self {
            kind: ActionErrorKind::Operation,
            request_id: Some(request_id),
            errors,
        }
    }

    pub(crate) fn transport(
        request_id: Option<&str>,
        code: impl Into<String>,
        error_type: OperationErrorType,
        message: impl Into<String>,
    ) -> Self {
        Self::single(
            ActionErrorKind::Transport,
            request_id,
            code,
            error_type,
            message,
        )
    }

    pub(crate) fn protocol(
        request_id: Option<&str>,
        code: impl Into<String>,
        message: impl Into<String>,
    ) -> Self {
        Self::single(
            ActionErrorKind::Protocol,
            request_id,
            code,
            OperationErrorType::InternalError,
            message,
        )
    }

    pub(crate) fn unexpected(
        request_id: Option<&str>,
        code: impl Into<String>,
        message: impl Into<String>,
    ) -> Self {
        Self::single(
            ActionErrorKind::Unexpected,
            request_id,
            code,
            OperationErrorType::InternalError,
            message,
        )
    }

    pub(crate) fn with_request_id(mut self, request_id: &str) -> Self {
        if self.request_id.is_none() {
            self.request_id = Some(request_id.to_owned());
        }

        self
    }

    fn single(
        kind: ActionErrorKind,
        request_id: Option<&str>,
        code: impl Into<String>,
        error_type: OperationErrorType,
        message: impl Into<String>,
    ) -> Self {
        Self {
            kind,
            request_id: request_id.map(str::to_owned),
            errors: vec![ActionErrorDetail::new(error_type, code, message)],
        }
    }
}
