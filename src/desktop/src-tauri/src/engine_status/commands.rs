use crate::engine_protocol::{
    EngineActionError, await_action_task, report_rust_originated_failure,
};
use crate::engine_status::EngineStatusState;
use tauri::State;

#[tauri::command]
pub(crate) async fn engine_check_status(
    state: State<'_, EngineStatusState>,
) -> Result<(), EngineActionError> {
    let engine_status_service = state.service();

    let result = await_action_task(move || engine_status_service.check_status()).await;

    report_rust_originated_failure(&result);

    result
}
