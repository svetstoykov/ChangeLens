pub mod engine_information;

use engine_information::{
    EngineClient, EngineCommandError, EngineInformation, EngineState, report_engine_command_failure,
};
use std::sync::Arc;
use tauri::State;

#[tauri::command]
async fn engine_get_info(
    state: State<'_, EngineState>,
) -> Result<EngineInformation, EngineCommandError> {
    let engine_information_service = state.service();

    let result = match tauri::async_runtime::spawn_blocking(move || {
        engine_information_service.get_information()
    })
    .await
    {
        Ok(result) => result,
        Err(error) => Err(EngineCommandError::new(
            "engine.commandFailed",
            format!("The engine command task failed: {error}"),
        )),
    };

    if let Err(error) = &result {
        report_engine_command_failure(error);
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
