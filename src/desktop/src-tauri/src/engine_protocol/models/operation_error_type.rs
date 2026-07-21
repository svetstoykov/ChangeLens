use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub enum OperationErrorType {
    NotFound,
    Validation,
    MalformedInput,
    UnprocessableInput,
    Conflict,
    InvalidOperation,
    Unauthorized,
    Timeout,
    ExternalDependencyFailure,
    InternalError,
}
