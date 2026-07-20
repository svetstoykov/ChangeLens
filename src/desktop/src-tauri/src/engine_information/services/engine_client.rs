use crate::engine_information::services::engine_process::EngineProcess;
use crate::engine_information::{EngineCommandError, EngineInformation, EngineInformationService};
use std::path::PathBuf;
use std::sync::Mutex;
use std::sync::atomic::{AtomicU64, Ordering};

pub struct EngineClient {
    engine_path: Option<PathBuf>,
    process: Mutex<Option<EngineProcess>>,
    next_request_id: AtomicU64,
}

impl EngineClient {
    pub fn new() -> Self {
        Self {
            engine_path: None,
            process: Mutex::new(None),
            next_request_id: AtomicU64::new(1),
        }
    }

    #[doc(hidden)]
    pub fn with_engine_path(engine_path: impl Into<PathBuf>) -> Self {
        Self {
            engine_path: Some(engine_path.into()),
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
            *process_guard = Some(EngineProcess::start(self.engine_path.as_deref())?);
        }

        let exchange_result = process_guard
            .as_mut()
            .ok_or_else(|| {
                EngineCommandError::new(
                    "engine.startFailed",
                    "The engine process did not become available.",
                )
            })?
            .get_information(&request_id);

        match exchange_result {
            Ok(information) => Ok(information),
            Err(exchange_error) => {
                if exchange_error.invalidates_process() {
                    *process_guard = None;
                }

                Err(exchange_error.into_command_error())
            }
        }
    }
}

impl Default for EngineClient {
    fn default() -> Self {
        Self::new()
    }
}

impl EngineInformationService for EngineClient {
    fn get_information(&self) -> Result<EngineInformation, EngineCommandError> {
        self.get_information()
    }
}
