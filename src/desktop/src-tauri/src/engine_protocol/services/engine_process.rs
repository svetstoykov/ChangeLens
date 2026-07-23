use crate::engine_protocol::services::{parse_response, resolve_engine_path};
use crate::engine_protocol::{EngineActionError, EngineExchangeError, OperationErrorType};
use std::io::{BufRead, BufReader, Read, Write};
use std::path::Path;
use std::process::{Child, ChildStdin, ChildStdout, Command, Stdio};
use std::sync::mpsc::{Receiver, RecvTimeoutError, SyncSender, sync_channel};
use std::thread::{self, JoinHandle};
use std::time::Duration;

const MAX_RESPONSE_BYTES: usize = 64 * 1024;

pub(crate) struct EngineProcess {
    child: Child,
    stdin: ChildStdin,
    response_receiver: Option<Receiver<Result<String, EngineActionError>>>,
    reader_thread: Option<JoinHandle<()>>,
}

impl EngineProcess {
    pub(crate) fn start(
        configured_engine_path: Option<&Path>,
        engine_arguments: &[String],
        dotnet_executable: &Path,
    ) -> Result<Self, EngineActionError> {
        let resolved_engine_path;
        let engine_path = if let Some(configured_engine_path) = configured_engine_path {
            configured_engine_path
        } else {
            resolved_engine_path = resolve_engine_path()?;
            &resolved_engine_path
        };
        let mut command = Command::new(dotnet_executable);
        command
            .arg(engine_path)
            .args(engine_arguments)
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::inherit());

        let mut child = command.spawn().map_err(|_| start_failed())?;
        let stdin = child.stdin.take().ok_or_else(start_failed)?;
        let stdout = child.stdout.take().ok_or_else(start_failed)?;
        let (response_sender, response_receiver) = sync_channel(1);
        let reader_thread = thread::spawn(move || read_responses(stdout, response_sender));

        Ok(Self {
            child,
            stdin,
            response_receiver: Some(response_receiver),
            reader_thread: Some(reader_thread),
        })
    }

    pub(crate) fn exchange<TRequest, TResult>(
        &mut self,
        request: &TRequest,
        request_id: &str,
        response_timeout: Duration,
    ) -> Result<TResult, EngineExchangeError>
    where
        TRequest: serde::Serialize,
        TResult: serde::de::DeserializeOwned,
    {
        let request_line = serialize_request(request, request_id)?;
        write_request(&mut self.stdin, &request_line, request_id)?;

        let response_line = self.receive_response(request_id, response_timeout)?;
        parse_response(&response_line, request_id)
    }

    pub(crate) fn id(&self) -> u32 {
        self.child.id()
    }

    fn receive_response(
        &self,
        request_id: &str,
        response_timeout: Duration,
    ) -> Result<String, EngineExchangeError> {
        let response_receiver = self.response_receiver.as_ref().ok_or_else(|| {
            EngineExchangeError::invalidating(EngineActionError::transport(
                Some(request_id),
                "engine.readFailed",
                OperationErrorType::ExternalDependencyFailure,
                "The engine response could not be read.",
            ))
        })?;

        match response_receiver.recv_timeout(response_timeout) {
            Ok(Ok(response_line)) => Ok(response_line),
            Ok(Err(error)) => Err(EngineExchangeError::invalidating(
                error.with_request_id(request_id),
            )),
            Err(RecvTimeoutError::Timeout) => Err(EngineExchangeError::invalidating(
                EngineActionError::transport(
                    Some(request_id),
                    "engine.responseTimedOut",
                    OperationErrorType::Timeout,
                    "The engine did not return a response before the deadline.",
                ),
            )),
            Err(RecvTimeoutError::Disconnected) => Err(EngineExchangeError::invalidating(
                EngineActionError::transport(
                    Some(request_id),
                    "engine.readFailed",
                    OperationErrorType::ExternalDependencyFailure,
                    "The engine response could not be read.",
                ),
            )),
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

fn start_failed() -> EngineActionError {
    EngineActionError::transport(
        None,
        "engine.startFailed",
        OperationErrorType::ExternalDependencyFailure,
        "The .NET engine process could not be started.",
    )
}

fn serialize_request<TRequest: serde::Serialize>(
    request: &TRequest,
    request_id: &str,
) -> Result<String, EngineExchangeError> {
    serde_json::to_string(request).map_err(|_| {
        EngineExchangeError::recoverable(EngineActionError::protocol(
            Some(request_id),
            "protocol.serializationFailed",
            "The desktop could not serialize the engine action request.",
        ))
    })
}

fn write_request(
    writer: &mut impl Write,
    request_line: &str,
    request_id: &str,
) -> Result<(), EngineExchangeError> {
    writeln!(writer, "{request_line}")
        .and_then(|()| writer.flush())
        .map_err(|_| {
            EngineExchangeError::invalidating(EngineActionError::transport(
                Some(request_id),
                "engine.writeFailed",
                OperationErrorType::ExternalDependencyFailure,
                "The engine action request could not be written.",
            ))
        })
}

fn read_responses(
    stdout: ChildStdout,
    response_sender: SyncSender<Result<String, EngineActionError>>,
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
                let _ = response_sender.send(Err(EngineActionError::transport(
                    None,
                    "engine.exited",
                    OperationErrorType::ExternalDependencyFailure,
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

fn read_bounded_line(reader: &mut impl BufRead) -> Result<Option<String>, EngineActionError> {
    let mut response_bytes = Vec::new();
    let bytes_read = reader
        .take((MAX_RESPONSE_BYTES + 1) as u64)
        .read_until(b'\n', &mut response_bytes)
        .map_err(|_| {
            EngineActionError::transport(
                None,
                "engine.readFailed",
                OperationErrorType::ExternalDependencyFailure,
                "The engine response could not be read.",
            )
        })?;

    if bytes_read == 0 {
        return Ok(None);
    }

    if bytes_read > MAX_RESPONSE_BYTES {
        return Err(EngineActionError::transport(
            None,
            "engine.responseTooLarge",
            OperationErrorType::ExternalDependencyFailure,
            "The engine response exceeds the 65536-byte limit.",
        ));
    }

    String::from_utf8(response_bytes).map(Some).map_err(|_| {
        EngineActionError::protocol(
            None,
            "protocol.invalidResponse",
            "The engine response is not valid UTF-8.",
        )
    })
}

#[cfg(test)]
mod tests {
    use super::{read_bounded_line, serialize_request, write_request};
    use serde::Serialize;
    use serde::ser::Error as _;
    use std::io::{self, BufRead, Cursor, Read, Write};

    struct RejectSerialization;

    impl Serialize for RejectSerialization {
        fn serialize<S>(&self, _serializer: S) -> Result<S::Ok, S::Error>
        where
            S: serde::Serializer,
        {
            Err(S::Error::custom("fixture serialization failure"))
        }
    }

    struct FailingWriter;

    impl Write for FailingWriter {
        fn write(&mut self, _buffer: &[u8]) -> io::Result<usize> {
            Err(io::Error::other("fixture write failure"))
        }

        fn flush(&mut self) -> io::Result<()> {
            Err(io::Error::other("fixture flush failure"))
        }
    }

    struct FailingReader;

    impl Read for FailingReader {
        fn read(&mut self, _buffer: &mut [u8]) -> io::Result<usize> {
            Err(io::Error::other("fixture read failure"))
        }
    }

    impl BufRead for FailingReader {
        fn fill_buf(&mut self) -> io::Result<&[u8]> {
            Err(io::Error::other("fixture read failure"))
        }

        fn consume(&mut self, _amount: usize) {}
    }

    #[test]
    fn accepts_response_at_size_limit() {
        let response = vec![b'a'; 64 * 1024];
        let line = read_bounded_line(&mut Cursor::new(response))
            .expect("the response at the limit must be readable")
            .expect("the response must contain one line");

        assert_eq!(line.len(), 64 * 1024);
    }

    #[test]
    fn rejects_response_over_size_limit() {
        let response = vec![b'a'; 64 * 1024 + 1];
        let error = read_bounded_line(&mut Cursor::new(response))
            .expect_err("the response over the limit must fail");

        assert_eq!(error.errors[0].code, "engine.responseTooLarge");
    }

    #[test]
    fn serialization_failure_does_not_invalidate_process() {
        let error = serialize_request(&RejectSerialization, "desktop-1")
            .expect_err("the fixture serializer must fail");

        assert!(!error.invalidates_process());
        assert_eq!(
            error.into_action_error().errors[0].code,
            "protocol.serializationFailed"
        );
    }

    #[test]
    fn write_failure_invalidates_process() {
        let error = write_request(&mut FailingWriter, "{}", "desktop-1")
            .expect_err("the fixture writer must fail");

        assert!(error.invalidates_process());
        assert_eq!(
            error.into_action_error().errors[0].code,
            "engine.writeFailed"
        );
    }

    #[test]
    fn read_failure_returns_stable_code() {
        let error =
            read_bounded_line(&mut FailingReader).expect_err("the fixture reader must fail");

        assert_eq!(error.errors[0].code, "engine.readFailed");
    }
}
