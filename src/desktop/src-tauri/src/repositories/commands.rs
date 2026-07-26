use crate::engine_protocol::{
    EngineActionError, OperationErrorType, await_action_task, report_rust_originated_failure,
};
use crate::repositories::{
    RepositoryFolderPickerState, RepositoryHistory, RepositoryOpenResult, RepositoryRestoreResult,
    RepositoryState,
};
use tauri::State;

#[tauri::command]
pub(crate) async fn select_repository_folder<R: tauri::Runtime>(
    window: tauri::WebviewWindow<R>,
    state: State<'_, RepositoryFolderPickerState>,
) -> Result<Option<String>, EngineActionError> {
    let folder_picker = state.picker();
    let result = match await_action_task(move || folder_picker.select_folder(&window)).await {
        Ok(Some(path)) => path.into_os_string().into_string().map(Some).map_err(|_| {
            EngineActionError::transport(
                None,
                "repository.pathEncodingUnsupported",
                OperationErrorType::UnprocessableInput,
                "The selected path cannot be represented as Unicode.",
            )
        }),
        Ok(None) => Ok(None),
        Err(error) => Err(error),
    };

    report_rust_originated_failure(&result);

    result
}

#[tauri::command]
pub(crate) async fn repository_open(
    state: State<'_, RepositoryState>,
    path: String,
) -> Result<RepositoryOpenResult, EngineActionError> {
    let repository_service = state.service();
    let result = await_action_task(move || repository_service.open_repository_record(&path)).await;

    report_rust_originated_failure(&result);

    result
}

#[tauri::command]
pub(crate) async fn repository_restore_last(
    state: State<'_, RepositoryState>,
) -> Result<RepositoryRestoreResult, EngineActionError> {
    let service = state.service();
    let result = await_action_task(move || service.restore_last_repository()).await;
    report_rust_originated_failure(&result);
    result
}

#[tauri::command]
pub(crate) async fn repository_list_recent(
    state: State<'_, RepositoryState>,
) -> Result<RepositoryHistory, EngineActionError> {
    let service = state.service();
    let result = await_action_task(move || service.list_recent_repositories()).await;
    report_rust_originated_failure(&result);
    result
}

#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn repository_remove_recent(
    state: State<'_, RepositoryState>,
    repository_id: String,
) -> Result<(), EngineActionError> {
    let service = state.service();
    let result = await_action_task(move || service.remove_recent_repository(&repository_id)).await;
    report_rust_originated_failure(&result);
    result
}
