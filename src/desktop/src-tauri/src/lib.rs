pub mod engine_protocol;
pub mod engine_status;
pub mod repositories;

use engine_protocol::{
    ActionErrorKind, EngineActionError, EngineClient, report_engine_action_failure,
};
use engine_status::EngineStatusState;
use std::sync::Arc;
use tauri::State;

#[tauri::command]
async fn engine_check_status(state: State<'_, EngineStatusState>) -> Result<(), EngineActionError> {
    let engine_status_service = state.service();

    let result =
        match tauri::async_runtime::spawn_blocking(move || engine_status_service.check_status())
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

pub fn configure_engine_status<R: tauri::Runtime>(
    builder: tauri::Builder<R>,
    state: EngineStatusState,
) -> tauri::Builder<R> {
    builder
        .manage(state)
        .invoke_handler(tauri::generate_handler![engine_check_status])
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    configure_engine_status(
        tauri::Builder::default(),
        EngineStatusState::new(Arc::new(EngineClient::new())),
    )
    .run(tauri::generate_context!())
    .expect("the ChangeLens desktop runtime could not be started");
}
