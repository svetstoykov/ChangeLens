use crate::engine_protocol::EngineActionError;
use crate::engine_protocol::constants::ENGINE_SHUTDOWN_FORCED_CODE;

pub(crate) fn report_engine_action_failure(error: &EngineActionError) {
    eprintln!("{}", create_engine_action_diagnostic(error));
}

pub(crate) fn report_engine_shutdown_forced() {
    eprintln!("{}", create_engine_shutdown_forced_diagnostic());
}

/// Reports why an engine response failed to satisfy the desktop's protocol shape.
///
/// The detail is the deserializer's own explanation, such as a missing or unknown property. It can
/// echo values taken from the engine response, so it is written only to the local diagnostic stream
/// and never returned to the user interface.
pub(crate) fn report_engine_response_schema_mismatch(action: &str, request_id: &str, detail: &str) {
    eprintln!(
        "{}",
        create_response_schema_mismatch_diagnostic(action, request_id, detail)
    );
}

fn create_engine_action_diagnostic(error: &EngineActionError) -> serde_json::Value {
    serde_json::json!({
        "event": "engine.actionFailed",
        "kind": error.kind,
        "requestId": error.request_id.as_deref(),
        "errorCodes": error
            .errors
            .iter()
            .map(|detail| detail.code.as_str())
            .collect::<Vec<_>>(),
    })
}

fn create_response_schema_mismatch_diagnostic(
    action: &str,
    request_id: &str,
    detail: &str,
) -> serde_json::Value {
    serde_json::json!({
        "event": "engine.responseSchemaMismatch",
        "action": action,
        "requestId": request_id,
        "detail": detail,
    })
}

fn create_engine_shutdown_forced_diagnostic() -> serde_json::Value {
    serde_json::json!({
        "event": ENGINE_SHUTDOWN_FORCED_CODE,
        "code": ENGINE_SHUTDOWN_FORCED_CODE,
    })
}

#[cfg(test)]
mod tests {
    use super::{
        create_engine_action_diagnostic, create_engine_shutdown_forced_diagnostic,
        create_response_schema_mismatch_diagnostic,
    };
    use crate::engine_protocol::{
        ActionErrorDetail, ActionErrorKind, EngineActionError, OperationErrorType,
    };

    #[test]
    fn creates_sanitized_structured_diagnostic() {
        let diagnostic = create_engine_action_diagnostic(&EngineActionError {
            kind: ActionErrorKind::Transport,
            request_id: Some("desktop-1".into()),
            errors: vec![ActionErrorDetail {
                error_type: OperationErrorType::ExternalDependencyFailure,
                code: "engine.readFailed".into(),
                message: "sensitive fixture detail".into(),
            }],
        });

        assert_eq!(diagnostic["event"], "engine.actionFailed");
        assert_eq!(diagnostic["kind"], "transport");
        assert_eq!(diagnostic["requestId"], "desktop-1");
        assert_eq!(diagnostic["errorCodes"][0], "engine.readFailed");
        assert!(diagnostic.get("message").is_none());
    }

    #[test]
    fn creates_actionable_response_schema_mismatch_diagnostic() {
        let diagnostic = create_response_schema_mismatch_diagnostic(
            "repositories.restoreLast",
            "desktop-5",
            "unknown field `preferredTarget`",
        );

        assert_eq!(diagnostic["event"], "engine.responseSchemaMismatch");
        assert_eq!(diagnostic["action"], "repositories.restoreLast");
        assert_eq!(diagnostic["requestId"], "desktop-5");
        assert_eq!(diagnostic["detail"], "unknown field `preferredTarget`");
    }

    #[test]
    fn creates_sanitized_forced_shutdown_diagnostic() {
        let diagnostic = create_engine_shutdown_forced_diagnostic();
        let fields = diagnostic
            .as_object()
            .expect("the diagnostic must be a structured object");

        assert_eq!(diagnostic["event"], "engine.shutdownForced");
        assert_eq!(diagnostic["code"], "engine.shutdownForced");
        assert_eq!(fields.len(), 2);
    }
}
