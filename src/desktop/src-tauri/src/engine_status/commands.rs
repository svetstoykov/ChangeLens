use crate::engine_protocol::{ActionErrorKind, EngineActionError, report_engine_action_failure};
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
