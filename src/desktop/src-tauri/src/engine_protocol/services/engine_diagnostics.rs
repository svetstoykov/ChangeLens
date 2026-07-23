use crate::engine_protocol::EngineActionError;
use crate::engine_protocol::constants::ENGINE_SHUTDOWN_FORCED_CODE;

pub(crate) fn report_engine_action_failure(error: &EngineActionError) {
    eprintln!("{}", create_engine_action_diagnostic(error));
}

pub(crate) fn report_engine_shutdown_forced() {
    eprintln!("{}", create_engine_shutdown_forced_diagnostic());
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

fn create_engine_shutdown_forced_diagnostic() -> serde_json::Value {
    serde_json::json!({
        "event": ENGINE_SHUTDOWN_FORCED_CODE,
        "code": ENGINE_SHUTDOWN_FORCED_CODE,
    })
}

#[cfg(test)]
mod tests {
    use super::{create_engine_action_diagnostic, create_engine_shutdown_forced_diagnostic};
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
