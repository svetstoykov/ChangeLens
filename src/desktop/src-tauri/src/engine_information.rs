use serde::{Deserialize, Serialize};
use std::env;
use std::io::{BufRead, BufReader, Write};
use std::path::{Path, PathBuf};
use std::process::{Child, ChildStdin, ChildStdout, Command, Stdio};
use std::sync::Mutex;
use std::sync::atomic::{AtomicU64, Ordering};

const CURRENT_PROTOCOL_VERSION: u32 = 1;
const ENGINE_INFORMATION_METHOD: &str = "engine.getInfo";

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineInformation {
    name: String,
    version: String,
    protocol_version: u32,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineCommandError {
    code: String,
    message: String,
}

impl EngineCommandError {
    pub fn new(code: impl Into<String>, message: impl Into<String>) -> Self {
        Self {
            code: code.into(),
            message: message.into(),
        }
    }
}

pub struct EngineClient {
    process: Mutex<Option<EngineProcess>>,
    next_request_id: AtomicU64,
}

impl EngineClient {
    pub fn new() -> Self {
        Self {
            process: Mutex::new(None),
            next_request_id: AtomicU64::new(1),
        }
    }

    pub fn get_information(&self) -> Result<EngineInformation, EngineCommandError> {
        let request_number = self.next_request_id.fetch_add(1, Ordering::Relaxed);
        let request_id = format!("desktop-{request_number}");
        let mut process_guard = self.process.lock().map_err(|_| {
            EngineCommandError::new(
                "engine.stateUnavailable",
                "The engine process state could not be acquired.",
            )
        })?;

        if process_guard.is_none() {
            *process_guard = Some(EngineProcess::start()?);
        }

        let process = process_guard.as_mut().ok_or_else(|| {
            EngineCommandError::new(
                "engine.startFailed",
                "The engine process did not become available.",
            )
        })?;

        process.get_information(&request_id)
    }
}

struct EngineProcess {
    child: Child,
    stdin: ChildStdin,
    stdout: BufReader<ChildStdout>,
}

impl EngineProcess {
    fn start() -> Result<Self, EngineCommandError> {
        let engine_path = resolve_engine_path()?;
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

        Ok(Self {
            child,
            stdin,
            stdout: BufReader::new(stdout),
        })
    }

    fn get_information(
        &mut self,
        request_id: &str,
    ) -> Result<EngineInformation, EngineCommandError> {
        let request = EngineRequest {
            protocol_version: CURRENT_PROTOCOL_VERSION,
            request_id,
            method: ENGINE_INFORMATION_METHOD,
        };
        let request_line = serde_json::to_string(&request).map_err(|error| {
            EngineCommandError::new(
                "protocol.serializationFailed",
                format!("The engine request could not be serialized: {error}"),
            )
        })?;

        writeln!(self.stdin, "{request_line}").map_err(|error| {
            EngineCommandError::new(
                "engine.writeFailed",
                format!("The engine request could not be written: {error}"),
            )
        })?;
        self.stdin.flush().map_err(|error| {
            EngineCommandError::new(
                "engine.writeFailed",
                format!("The engine request could not be flushed: {error}"),
            )
        })?;

        let mut response_line = String::new();
        let bytes_read = self.stdout.read_line(&mut response_line).map_err(|error| {
            EngineCommandError::new(
                "engine.readFailed",
                format!("The engine response could not be read: {error}"),
            )
        })?;

        if bytes_read == 0 {
            return Err(EngineCommandError::new(
                "engine.exited",
                "The engine exited before returning a response.",
            ));
        }

        let response: EngineResponse = serde_json::from_str(&response_line).map_err(|error| {
            EngineCommandError::new(
                "protocol.invalidResponse",
                format!("The engine returned an invalid response: {error}"),
            )
        })?;

        validate_response(&response, request_id)?;

        match response.response_type.as_str() {
            "result" => response.result.ok_or_else(|| {
                EngineCommandError::new(
                    "protocol.invalidResponse",
                    "The successful engine response does not contain a result.",
                )
            }),
            "error" => Err(response.error.unwrap_or_else(|| {
                EngineCommandError::new(
                    "protocol.invalidResponse",
                    "The failed engine response does not contain an error.",
                )
            })),
            response_type => Err(EngineCommandError::new(
                "protocol.invalidResponse",
                format!("The engine response type '{response_type}' is not recognized."),
            )),
        }
    }
}

impl Drop for EngineProcess {
    fn drop(&mut self) {
        let _ = self.child.kill();
        let _ = self.child.wait();
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct EngineRequest<'a> {
    protocol_version: u32,
    request_id: &'a str,
    method: &'a str,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct EngineResponse {
    protocol_version: u32,
    #[serde(rename = "type")]
    response_type: String,
    request_id: Option<String>,
    result: Option<EngineInformation>,
    error: Option<EngineCommandError>,
}

fn validate_response(
    response: &EngineResponse,
    request_id: &str,
) -> Result<(), EngineCommandError> {
    if response.protocol_version != CURRENT_PROTOCOL_VERSION {
        return Err(EngineCommandError::new(
            "protocol.unsupportedVersion",
            format!(
                "The engine responded with unsupported protocol version {}.",
                response.protocol_version
            ),
        ));
    }

    if response.request_id.as_deref() != Some(request_id) {
        return Err(EngineCommandError::new(
            "protocol.correlationMismatch",
            "The engine response does not match the request identifier.",
        ));
    }

    Ok(())
}

fn resolve_engine_path() -> Result<PathBuf, EngineCommandError> {
    if let Some(configured_path) = env::var_os("CHANGELENS_ENGINE_PATH") {
        return Ok(PathBuf::from(configured_path));
    }

    if cfg!(debug_assertions) {
        return Ok(Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../engine/ChangeLens.Engine/bin/Debug/net10.0/ChangeLens.Engine.dll"));
    }

    Err(EngineCommandError::new(
        "engine.pathUnavailable",
        "CHANGELENS_ENGINE_PATH must point to the packaged engine in release builds.",
    ))
}

#[cfg(test)]
mod tests {
    use super::EngineClient;

    #[test]
    fn gets_information_from_the_real_dotnet_engine() {
        let engine_client = EngineClient::new();

        let information = engine_client
            .get_information()
            .expect("the development engine should return its information");

        assert_eq!(information.name, "ChangeLens.Engine");
        assert_eq!(information.version, "0.1.0");
        assert_eq!(information.protocol_version, 1);
    }
}
