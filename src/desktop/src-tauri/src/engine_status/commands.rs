use crate::engine_protocol::{
    EngineActionError, action_task_failed, report_rust_originated_failure,
};
use crate::engine_status::EngineStatusState;
use tauri::State;

#[tauri::command]
pub(crate) async fn engine_check_status(
    state: State<'_, EngineStatusState>,
) -> Result<(), EngineActionError> {
    let engine_status_service = state.service();

    let result =
        match tauri::async_runtime::spawn_blocking(move || engine_status_service.check_status())
            .await
        {
            Ok(result) => result,
            Err(_) => Err(action_task_failed()),
        };

    report_rust_originated_failure(&result);

    result
}
