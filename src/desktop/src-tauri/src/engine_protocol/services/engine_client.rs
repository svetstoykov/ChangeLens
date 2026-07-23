use super::report_engine_shutdown_forced;
use crate::engine_protocol::constants::{ENGINE_SHUTTING_DOWN_CODE, ENGINE_SHUTTING_DOWN_MESSAGE};
use crate::engine_protocol::{
    ActionErrorDetail, EngineActionError, EngineProcess, EngineProtocolRequest,
    EngineShutdownOutcome, OperationErrorType,
};
use std::path::PathBuf;
use std::sync::Mutex;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::time::Duration;

const CURRENT_PROTOCOL_VERSION: u32 = 1;

pub struct EngineClient {
    engine_path: Option<PathBuf>,
    engine_arguments: Vec<String>,
    dotnet_executable: PathBuf,
    process: Mutex<Option<EngineProcess>>,
    next_request_id: AtomicU64,
    shutting_down: AtomicBool,
}

impl EngineClient {
    pub fn new() -> Self {
        Self::configured(None, Vec::new(), PathBuf::from("dotnet"))
    }

    #[doc(hidden)]
    pub fn with_engine_path(engine_path: impl Into<PathBuf>) -> Self {
        Self::configured(
            Some(engine_path.into()),
            Vec::new(),
            PathBuf::from("dotnet"),
        )
    }

    #[doc(hidden)]
    pub fn with_engine_path_and_arguments(
        engine_path: impl Into<PathBuf>,
        engine_arguments: Vec<String>,
    ) -> Self {
        Self::configured(
            Some(engine_path.into()),
            engine_arguments,
            PathBuf::from("dotnet"),
        )
    }

    #[doc(hidden)]
    pub fn with_process_configuration(
        engine_path: impl Into<PathBuf>,
        engine_arguments: Vec<String>,
        dotnet_executable: PathBuf,
    ) -> Self {
        Self::configured(
            Some(engine_path.into()),
            engine_arguments,
            dotnet_executable,
        )
    }

    pub(crate) fn execute_action<TParameters, TResult>(
        &self,
        action: &str,
        parameters: Option<TParameters>,
        response_timeout: Duration,
    ) -> Result<TResult, EngineActionError>
    where
        TParameters: serde::Serialize,
        TResult: serde::de::DeserializeOwned,
    {
        if self.shutting_down.load(Ordering::Acquire) {
            return Err(shutting_down_error(None));
        }

        let request_number = self.next_request_id.fetch_add(1, Ordering::Relaxed);
        let request_id = format!("desktop-{request_number}");
        let mut process_guard = self.process.lock().map_err(|_| {
            EngineActionError::unexpected(
                Some(&request_id),
                "desktop.engineStateUnavailable",
                "The desktop could not acquire the engine process state.",
            )
        })?;

        if self.shutting_down.load(Ordering::Acquire) {
            return Err(shutting_down_error(Some(&request_id)));
        }

        if process_guard.is_none() {
            *process_guard = Some(EngineProcess::start(
                self.engine_path.as_deref(),
                &self.engine_arguments,
                &self.dotnet_executable,
            )?);
        }

        let request = EngineProtocolRequest {
            protocol_version: CURRENT_PROTOCOL_VERSION,
            request_id: &request_id,
            action,
            parameters,
        };
        let exchange_result = process_guard
            .as_mut()
            .expect("the engine process was initialized")
            .exchange::<_, TResult>(&request, &request_id, response_timeout);

        match exchange_result {
            Ok(result) => Ok(result),
            Err(exchange_error) => {
                if exchange_error.invalidates_process() {
                    invalidate_process(process_guard.take());
                }

                Err(exchange_error.into_action_error())
            }
        }
    }

    #[doc(hidden)]
    pub fn process_id_for_testing(&self) -> Option<u32> {
        self.process
            .lock()
            .ok()
            .and_then(|process| process.as_ref().map(EngineProcess::id))
    }

    /// Gracefully shuts down the owned Engine process within a bounded grace period.
    pub fn shutdown(&self) {
        self.shutting_down.store(true, Ordering::Release);
        let mut process_guard = self
            .process
            .lock()
            .unwrap_or_else(|poisoned| poisoned.into_inner());
        shutdown_process(process_guard.take());
    }

    fn configured(
        engine_path: Option<PathBuf>,
        engine_arguments: Vec<String>,
        dotnet_executable: PathBuf,
    ) -> Self {
        Self {
            engine_path,
            engine_arguments,
            dotnet_executable,
            process: Mutex::new(None),
            next_request_id: AtomicU64::new(1),
            shutting_down: AtomicBool::new(false),
        }
    }
}

impl Drop for EngineClient {
    fn drop(&mut self) {
        self.shutdown();
    }
}

impl Default for EngineClient {
    fn default() -> Self {
        Self::new()
    }
}

fn shutting_down_error(request_id: Option<&str>) -> EngineActionError {
    EngineActionError::operation(
        request_id.map(str::to_owned),
        vec![ActionErrorDetail::new(
            OperationErrorType::InvalidOperation,
            ENGINE_SHUTTING_DOWN_CODE,
            ENGINE_SHUTTING_DOWN_MESSAGE,
        )],
    )
}

fn shutdown_process(process: Option<EngineProcess>) {
    let Some(mut process) = process else {
        return;
    };

    if process.shutdown() == EngineShutdownOutcome::Forced {
        report_engine_shutdown_forced();
    }
}

fn invalidate_process(process: Option<EngineProcess>) {
    let Some(mut process) = process else {
        return;
    };

    process.force_shutdown();
}
