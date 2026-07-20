use crate::engine_information::models::{EngineExchangeError, EngineRequest, EngineResponse};
use crate::engine_information::services::engine_path::resolve_engine_path;
use crate::engine_information::{EngineCommandError, EngineInformation};
use std::io::{BufRead, BufReader, Read, Write};
use std::path::Path;
use std::process::{Child, ChildStdin, ChildStdout, Command, Stdio};
use std::sync::mpsc::{Receiver, RecvTimeoutError, SyncSender, sync_channel};
use std::thread::{self, JoinHandle};
use std::time::Duration;

const CURRENT_PROTOCOL_VERSION: u32 = 1;
const ENGINE_INFORMATION_METHOD: &str = "engine.getInfo";
const ENGINE_NAME: &str = "ChangeLens.Engine";
const MAX_RESPONSE_BYTES: usize = 64 * 1024;
const RESPONSE_TIMEOUT: Duration = Duration::from_secs(5);

pub(crate) struct EngineProcess {
    child: Child,
    stdin: ChildStdin,
    response_receiver: Option<Receiver<Result<String, EngineCommandError>>>,
    reader_thread: Option<JoinHandle<()>>,
}

impl EngineProcess {
    pub(crate) fn start(configured_engine_path: Option<&Path>) -> Result<Self, EngineCommandError> {
        let resolved_engine_path;
        let engine_path = if let Some(configured_engine_path) = configured_engine_path {
            configured_engine_path
        } else {
            resolved_engine_path = resolve_engine_path()?;
            &resolved_engine_path
        };
        let mut command = Command::new("dotnet");
        command
            .arg(engine_path)
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::inherit());

        let mut child = command.spawn().map_err(|error| {
            EngineCommandError::new(
                "engine.startFailed",
                format!("The .NET engine process could not be started: {error}"),
            )
        })?;
        let stdin = child.stdin.take().ok_or_else(|| {
            EngineCommandError::new(
                "engine.startFailed",
                "The engine standard-input stream is unavailable.",
            )
        })?;
        let stdout = child.stdout.take().ok_or_else(|| {
            EngineCommandError::new(
                "engine.startFailed",
                "The engine standard-output stream is unavailable.",
            )
        })?;
        let (response_sender, response_receiver) = sync_channel(1);
        let reader_thread = thread::spawn(move || read_responses(stdout, response_sender));

        Ok(Self {
            child,
            stdin,
            response_receiver: Some(response_receiver),
            reader_thread: Some(reader_thread),
        })
    }

    pub(crate) fn get_information(
        &mut self,
        request_id: &str,
    ) -> Result<EngineInformation, EngineExchangeError> {
        let request = EngineRequest {
            protocol_version: CURRENT_PROTOCOL_VERSION,
            request_id,
            method: ENGINE_INFORMATION_METHOD,
        };
        let request_line = serde_json::to_string(&request).map_err(|error| {
            EngineExchangeError::recoverable(EngineCommandError::new(
                "protocol.serializationFailed",
                format!("The engine request could not be serialized: {error}"),
            ))
        })?;

        writeln!(self.stdin, "{request_line}").map_err(|error| {
            EngineExchangeError::invalidating(EngineCommandError::new(
                "engine.writeFailed",
                format!("The engine request could not be written: {error}"),
            ))
        })?;
        self.stdin.flush().map_err(|error| {
            EngineExchangeError::invalidating(EngineCommandError::new(
                "engine.writeFailed",
                format!("The engine request could not be flushed: {error}"),
            ))
        })?;

        let response_line = self.receive_response()?;
        parse_response(&response_line, request_id)
    }

    fn receive_response(&self) -> Result<String, EngineExchangeError> {
        let response_receiver = self.response_receiver.as_ref().ok_or_else(|| {
            EngineExchangeError::invalidating(EngineCommandError::new(
                "engine.readFailed",
                "The engine response channel is unavailable.",
            ))
        })?;

        match response_receiver.recv_timeout(RESPONSE_TIMEOUT) {
            Ok(Ok(response_line)) => Ok(response_line),
            Ok(Err(error)) => Err(EngineExchangeError::invalidating(error)),
            Err(RecvTimeoutError::Timeout) => {
                Err(EngineExchangeError::invalidating(EngineCommandError::new(
                    "engine.responseTimedOut",
                    "The engine did not return a response before the deadline.",
                )))
            }
            Err(RecvTimeoutError::Disconnected) => {
                Err(EngineExchangeError::invalidating(EngineCommandError::new(
                    "engine.readFailed",
                    "The engine response channel closed unexpectedly.",
                )))
            }
        }
    }
}

impl Drop for EngineProcess {
    fn drop(&mut self) {
        self.response_receiver.take();
        let _ = self.child.kill();
        let _ = self.child.wait();

        if let Some(reader_thread) = self.reader_thread.take() {
            let _ = reader_thread.join();
        }
    }
}

fn read_responses(
    stdout: ChildStdout,
    response_sender: SyncSender<Result<String, EngineCommandError>>,
) {
    let mut reader = BufReader::new(stdout);

    loop {
        match read_bounded_line(&mut reader) {
            Ok(Some(response_line)) => {
                if response_sender.send(Ok(response_line)).is_err() {
                    return;
                }
            }
            Ok(None) => {
                let _ = response_sender.send(Err(EngineCommandError::new(
                    "engine.exited",
                    "The engine exited before returning a response.",
                )));
                return;
            }
            Err(error) => {
                let _ = response_sender.send(Err(error));
                return;
            }
        }
    }
}

fn read_bounded_line(reader: &mut impl BufRead) -> Result<Option<String>, EngineCommandError> {
    let mut response_bytes = Vec::new();
    let bytes_read = reader
        .take((MAX_RESPONSE_BYTES + 1) as u64)
        .read_until(b'\n', &mut response_bytes)
        .map_err(|error| {
            EngineCommandError::new(
                "engine.readFailed",
                format!("The engine response could not be read: {error}"),
            )
        })?;

    if bytes_read == 0 {
        return Ok(None);
    }

    if bytes_read > MAX_RESPONSE_BYTES {
        return Err(EngineCommandError::new(
            "engine.responseTooLarge",
            format!("The engine response exceeds the {MAX_RESPONSE_BYTES}-byte limit."),
        ));
    }

    String::from_utf8(response_bytes).map(Some).map_err(|_| {
        EngineCommandError::new(
            "protocol.invalidResponse",
            "The engine response is not valid UTF-8.",
        )
    })
}

fn parse_response(
    response_line: &str,
    request_id: &str,
) -> Result<EngineInformation, EngineExchangeError> {
    let response: EngineResponse = serde_json::from_str(response_line).map_err(|error| {
        EngineExchangeError::invalidating(EngineCommandError::new(
            "protocol.invalidResponse",
            format!("The engine returned an invalid response: {error}"),
        ))
    })?;

    match response {
        EngineResponse::Result {
            protocol_version,
            request_id: response_request_id,
            result,
        } => {
            validate_response_metadata(
                protocol_version,
                Some(response_request_id.as_str()),
                request_id,
            )?;

            if result.name != ENGINE_NAME
                || result.version.is_empty()
                || result.protocol_version != CURRENT_PROTOCOL_VERSION
            {
                return Err(invalid_response(
                    "The engine result does not match the protocol schema.",
                ));
            }

            Ok(result)
        }
        EngineResponse::Error {
            protocol_version,
            request_id: response_request_id,
            error,
        } => {
            let response_request_id = match &response_request_id {
                serde_json::Value::String(value) => Some(value.as_str()),
                serde_json::Value::Null => None,
                _ => {
                    return Err(invalid_response(
                        "The engine error request identifier has an invalid type.",
                    ));
                }
            };

            validate_response_metadata(protocol_version, response_request_id, request_id)?;

            if error.code.is_empty() || error.message.is_empty() {
                return Err(invalid_response(
                    "The engine error does not match the protocol schema.",
                ));
            }

            Err(EngineExchangeError::recoverable(error))
        }
    }
}

fn validate_response_metadata(
    protocol_version: u32,
    response_request_id: Option<&str>,
    expected_request_id: &str,
) -> Result<(), EngineExchangeError> {
    if protocol_version != CURRENT_PROTOCOL_VERSION {
        return Err(EngineExchangeError::invalidating(EngineCommandError::new(
            "protocol.unsupportedVersion",
            format!("The engine responded with unsupported protocol version {protocol_version}."),
        )));
    }

    if response_request_id != Some(expected_request_id) {
        return Err(EngineExchangeError::invalidating(EngineCommandError::new(
            "protocol.correlationMismatch",
            "The engine response does not match the request identifier.",
        )));
    }

    Ok(())
}

fn invalid_response(message: impl Into<String>) -> EngineExchangeError {
    EngineExchangeError::invalidating(EngineCommandError::new("protocol.invalidResponse", message))
}

#[cfg(test)]
mod tests {
    use super::{MAX_RESPONSE_BYTES, parse_response, read_bounded_line};
    use std::io::Cursor;

    #[test]
    fn rejects_response_with_missing_required_field() {
        assert_invalid_response(r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1"}"#);
    }

    #[test]
    fn rejects_response_with_unknown_field() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1","result":{"name":"ChangeLens.Engine","version":"0.1.0","protocolVersion":1},"extra":true}"#,
        );
    }

    #[test]
    fn rejects_response_with_conflicting_result_and_error_fields() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1","result":{"name":"ChangeLens.Engine","version":"0.1.0","protocolVersion":1},"error":{"code":"engine.failed","message":"failed"}}"#,
        );
    }

    #[test]
    fn rejects_error_response_with_conflicting_result_and_error_fields() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"error","requestId":"desktop-1","result":{"name":"ChangeLens.Engine","version":"0.1.0","protocolVersion":1},"error":{"code":"engine.failed","message":"failed"}}"#,
        );
    }

    #[test]
    fn rejects_result_with_unknown_nested_field() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1","result":{"name":"ChangeLens.Engine","version":"0.1.0","protocolVersion":1,"extra":true}}"#,
        );
    }

    #[test]
    fn rejects_result_with_missing_nested_required_field() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1","result":{"name":"ChangeLens.Engine","protocolVersion":1}}"#,
        );
    }

    #[test]
    fn rejects_result_that_violates_nested_value_constraints() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"result","requestId":"desktop-1","result":{"name":"other-engine","version":"","protocolVersion":2}}"#,
        );
    }

    #[test]
    fn rejects_error_with_missing_required_request_identifier() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"error","error":{"code":"engine.failed","message":"failed"}}"#,
        );
    }

    #[test]
    fn rejects_error_with_missing_required_error() {
        assert_invalid_response(r#"{"protocolVersion":1,"type":"error","requestId":"desktop-1"}"#);
    }

    #[test]
    fn rejects_error_with_empty_required_value() {
        assert_invalid_response(
            r#"{"protocolVersion":1,"type":"error","requestId":"desktop-1","error":{"code":"","message":"failed"}}"#,
        );
    }

    #[test]
    fn rejects_oversized_response_line() {
        let oversized_line = vec![b'a'; MAX_RESPONSE_BYTES + 1];
        let error = read_bounded_line(&mut Cursor::new(oversized_line))
            .expect_err("an oversized response must be rejected");

        assert_eq!(error.code, "engine.responseTooLarge");
    }

    fn assert_invalid_response(response: &str) {
        let error = parse_response(response, "desktop-1")
            .expect_err("the response must be rejected")
            .into_command_error();

        assert_eq!(error.code, "protocol.invalidResponse");
    }
}
