use crate::engine_protocol::{ActionErrorKind, EngineActionError, report_engine_action_failure};

/// Creates the sanitized error returned when a desktop command task cannot complete.
pub(crate) fn action_task_failed() -> EngineActionError {
    EngineActionError::unexpected(
        None,
        "desktop.actionTaskFailed",
        "The desktop could not complete the engine action task.",
    )
}

/// Reports desktop-originated failures while preserving Engine operation errors without relogging.
pub(crate) fn report_rust_originated_failure<T>(result: &Result<T, EngineActionError>) {
    if let Err(error) = result
        && error.kind != ActionErrorKind::Operation
    {
        report_engine_action_failure(error);
    }
}
