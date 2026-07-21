use crate::engine_information::constants::{
    CURRENT_PROTOCOL_VERSION, ENGINE_INFORMATION_METHOD, ENGINE_NAME,
};
use crate::engine_information::{
    EngineInformation, EngineInformationParameters, EngineInformationService,
};
use crate::engine_protocol::{
    EngineActionError, EngineExchangeError, EngineProcess, EngineProtocolRequest,
};
use std::path::PathBuf;
use std::sync::Mutex;
use std::sync::atomic::{AtomicU64, Ordering};

pub struct EngineClient {
    engine_path: Option<PathBuf>,
    engine_arguments: Vec<String>,
    dotnet_executable: PathBuf,
    process: Mutex<Option<EngineProcess>>,
    next_request_id: AtomicU64,
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

    pub fn get_information(&self) -> Result<EngineInformation, EngineActionError> {
        let request_number = self.next_request_id.fetch_add(1, Ordering::Relaxed);
        let request_id = format!("desktop-{request_number}");
        let mut process_guard = self.process.lock().map_err(|_| {
            EngineActionError::unexpected(
                Some(&request_id),
                "desktop.engineStateUnavailable",
                "The desktop could not acquire the engine process state.",
            )
        })?;

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
            method: ENGINE_INFORMATION_METHOD,
            params: EngineInformationParameters {},
        };
        let exchange_result = process_guard
            .as_mut()
            .expect("the engine process was initialized")
            .exchange::<_, EngineInformation>(&request, &request_id)
            .and_then(|information| validate_information(information, &request_id));

        match exchange_result {
            Ok(information) => Ok(information),
            Err(exchange_error) => {
                if exchange_error.invalidates_process() {
                    *process_guard = None;
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
        }
    }
}

impl Default for EngineClient {
    fn default() -> Self {
        Self::new()
    }
}

impl EngineInformationService for EngineClient {
    fn get_information(&self) -> Result<EngineInformation, EngineActionError> {
        self.get_information()
    }
}

fn validate_information(
    information: EngineInformation,
    request_id: &str,
) -> Result<EngineInformation, EngineExchangeError> {
    if information.name == ENGINE_NAME
        && !information.version.trim().is_empty()
        && information.protocol_version == CURRENT_PROTOCOL_VERSION
    {
        return Ok(information);
    }

    Err(EngineExchangeError::invalidating(
        EngineActionError::protocol(
            Some(request_id),
            "protocol.invalidResponse",
            "The engine information result does not match the protocol schema.",
        ),
    ))
}
