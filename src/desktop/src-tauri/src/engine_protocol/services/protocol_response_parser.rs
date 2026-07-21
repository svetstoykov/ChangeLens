use crate::engine_protocol::{EngineActionError, EngineExchangeError, EngineResponse};
use serde::de::DeserializeOwned;

const CURRENT_PROTOCOL_VERSION: u32 = 1;

pub(crate) fn parse_response<T: DeserializeOwned>(
    response_line: &str,
    expected_request_id: &str,
) -> Result<T, EngineExchangeError> {
    let response: EngineResponse<T> = serde_json::from_str(response_line).map_err(|_| {
        invalid_response(
            Some(expected_request_id),
            "The engine returned a response that does not match the protocol schema.",
        )
    })?;

    match response {
        EngineResponse::Result {
            protocol_version,
            request_id,
            result,
        } => {
            validate_metadata(
                protocol_version,
                Some(request_id.as_str()),
                expected_request_id,
            )?;
            Ok(result)
        }
        EngineResponse::Error {
            protocol_version,
            request_id,
            errors,
        } => {
            validate_metadata(protocol_version, request_id.as_deref(), expected_request_id)?;

            if errors.is_empty() || errors.iter().any(|error| !error.is_valid()) {
                return Err(invalid_response(
                    Some(expected_request_id),
                    "The engine error response does not match the protocol schema.",
                ));
            }

            Err(EngineExchangeError::recoverable(
                EngineActionError::operation(expected_request_id.to_owned(), errors),
            ))
        }
    }
}

fn validate_metadata(
    protocol_version: u32,
    response_request_id: Option<&str>,
    expected_request_id: &str,
) -> Result<(), EngineExchangeError> {
    if protocol_version != CURRENT_PROTOCOL_VERSION {
        return Err(EngineExchangeError::invalidating(
            EngineActionError::protocol(
                Some(expected_request_id),
                "protocol.unsupportedVersion",
                "The engine responded with an unsupported protocol version.",
            ),
        ));
    }

    if response_request_id != Some(expected_request_id) {
        return Err(EngineExchangeError::invalidating(
            EngineActionError::protocol(
                Some(expected_request_id),
                "protocol.correlationMismatch",
                "The engine response does not match the request identifier.",
            ),
        ));
    }

    Ok(())
}

fn invalid_response(request_id: Option<&str>, message: &str) -> EngineExchangeError {
    EngineExchangeError::invalidating(EngineActionError::protocol(
        request_id,
        "protocol.invalidResponse",
        message,
    ))
}

#[cfg(test)]
mod tests {
    use super::parse_response;
    use crate::engine_protocol::{ActionErrorKind, OperationErrorType};

    const STATUS_RESULT_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/engine-check-status.result.json"
    ));
    const ERROR_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/ordered-errors.response.json"
    ));
    #[test]
    fn preserves_shared_ordered_error_fixture() {
        let error = parse_response::<()>(ERROR_FIXTURE, "desktop-43")
            .expect_err("the canonical error fixture must reject the action")
            .into_action_error();

        assert_eq!(error.kind, ActionErrorKind::Operation);
        assert_eq!(error.request_id.as_deref(), Some("desktop-43"));
        assert_eq!(error.errors.len(), 2);
        assert_eq!(error.errors[0].error_type, OperationErrorType::Validation);
        assert_eq!(error.errors[0].code, "fixture.first");
        assert_eq!(error.errors[1].error_type, OperationErrorType::Conflict);
        assert_eq!(error.errors[1].code, "fixture.second");
    }

    #[test]
    fn parses_shared_payload_free_result_fixture() {
        let result = parse_response::<()>(STATUS_RESULT_FIXTURE, "desktop-42")
            .expect("the canonical payload-free result must parse");

        assert_eq!(result, ());
    }

    #[test]
    fn rejects_empty_errors_and_invalid_metadata() {
        for response in [
            r#"{"protocolVersion":1,"type":"error","requestId":"desktop-1","errors":[]}"#,
            r#"{"protocolVersion":2,"type":"result","requestId":"desktop-1","result":null}"#,
            r#"{"protocolVersion":1,"type":"result","requestId":"other","result":null}"#,
            r#"{"protocolVersion":1,"type":"error","requestId":"desktop-1","errors":[{"type":"Unknown","code":"fixture","message":"bad"}]}"#,
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1"}"#,
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1","result":null,"extra":true}"#,
            r#"{"protocolVersion":"1","type":"result","requestId":"desktop-1","result":null}"#,
        ] {
            let error = parse_response::<()>(response, "desktop-1")
                .expect_err("the invalid response must be rejected")
                .into_action_error();

            assert_eq!(error.kind, ActionErrorKind::Protocol);
        }
    }
}
