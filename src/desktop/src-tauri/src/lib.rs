pub mod engine_information;
pub mod engine_protocol;

use engine_information::{EngineClient, EngineInformation, EngineState};
use engine_protocol::{ActionErrorKind, EngineActionError, report_engine_action_failure};
use std::sync::Arc;
use tauri::State;

#[tauri::command]
async fn engine_get_info(
    state: State<'_, EngineState>,
) -> Result<EngineInformation, EngineActionError> {
    let engine_information_service = state.service();

    let result = match tauri::async_runtime::spawn_blocking(move || {
        engine_information_service.get_information()
    })
    .await
    {
        Ok(result) => result,
        Err(_) => Err(EngineActionError::unexpected(
            None,
            "desktop.actionTaskFailed",
            "The desktop could not complete the engine action task.",
        )),
    };

    if let Err(error) = &result
        && error.kind != ActionErrorKind::Operation
    {
        report_engine_action_failure(error);
    }

    result
}

pub fn configure_engine_information<R: tauri::Runtime>(
    builder: tauri::Builder<R>,
    state: EngineState,
) -> tauri::Builder<R> {
    builder
        .manage(state)
        .invoke_handler(tauri::generate_handler![engine_get_info])
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    configure_engine_information(
        tauri::Builder::default(),
        EngineState::new(Arc::new(EngineClient::new())),
    )
    .run(tauri::generate_context!())
    .expect("the ChangeLens desktop runtime could not be started");
}
