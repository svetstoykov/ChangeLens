use crate::engine_information::EngineCommandError;

pub(crate) fn report_engine_command_failure(error: &EngineCommandError) {
    eprintln!("{}", create_engine_command_diagnostic(error));
}

fn create_engine_command_diagnostic(error: &EngineCommandError) -> serde_json::Value {
    serde_json::json!({
        "event": "engine.commandFailed",
        "errorCode": error.code,
    })
}

#[cfg(test)]
mod tests {
    use super::create_engine_command_diagnostic;
    use crate::engine_information::EngineCommandError;

    #[test]
    fn creates_sanitized_structured_diagnostic() {
        let diagnostic = create_engine_command_diagnostic(&EngineCommandError::new(
            "engine.readFailed",
            "sensitive transport detail",
        ));

        assert_eq!(diagnostic["event"], "engine.commandFailed");
        assert_eq!(diagnostic["errorCode"], "engine.readFailed");
        assert!(diagnostic.get("message").is_none());
    }
}
