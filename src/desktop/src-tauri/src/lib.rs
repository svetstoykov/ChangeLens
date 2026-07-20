mod engine_information;

use engine_information::{EngineClient, EngineCommandError, EngineInformation};
use std::sync::Arc;
use tauri::State;

struct EngineState(Arc<EngineClient>);

#[tauri::command]
async fn engine_get_info(
    state: State<'_, EngineState>,
) -> Result<EngineInformation, EngineCommandError> {
    let engine_client = Arc::clone(&state.0);

    tauri::async_runtime::spawn_blocking(move || engine_client.get_information())
        .await
        .map_err(|error| {
            EngineCommandError::new(
                "engine.commandFailed",
                format!("The engine command task failed: {error}"),
            )
        })?
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(EngineState(Arc::new(EngineClient::new())))
        .invoke_handler(tauri::generate_handler![engine_get_info])
        .run(tauri::generate_context!())
        .expect("the ChangeLens desktop runtime could not be started");
}
