use crate::engine_protocol::{
    ActionErrorKind, EngineActionError, OperationErrorType, report_engine_action_failure,
};
use crate::repositories::{RepositoryDescriptor, RepositoryFolderPickerState, RepositoryState};
use tauri::State;

#[tauri::command]
pub(crate) async fn select_repository_folder(
    state: State<'_, RepositoryFolderPickerState>,
) -> Result<Option<String>, EngineActionError> {
    let folder_picker = state.picker();
    let result =
        match tauri::async_runtime::spawn_blocking(move || folder_picker.select_folder()).await {
            Ok(Ok(Some(path))) => path.into_os_string().into_string().map(Some).map_err(|_| {
                EngineActionError::transport(
                    None,
                    "repository.pathEncodingUnsupported",
                    OperationErrorType::UnprocessableInput,
                    "The selected path cannot be represented as Unicode.",
                )
            }),
            Ok(Ok(None)) => Ok(None),
            Ok(Err(error)) => Err(error),
            Err(_) => Err(action_task_failed()),
        };

    report_rust_originated_failure(&result);

    result
}

#[tauri::command]
pub(crate) async fn repository_open(
    state: State<'_, RepositoryState>,
    path: String,
) -> Result<RepositoryDescriptor, EngineActionError> {
    let repository_service = state.service();
    let result = match tauri::async_runtime::spawn_blocking(move || {
        repository_service.open_repository(&path)
    })
    .await
    {
        Ok(result) => result,
        Err(_) => Err(action_task_failed()),
    };

    report_rust_originated_failure(&result);

    result
}

fn action_task_failed() -> EngineActionError {
    EngineActionError::unexpected(
        None,
        "desktop.actionTaskFailed",
        "The desktop could not complete the engine action task.",
    )
}

fn report_rust_originated_failure<T>(result: &Result<T, EngineActionError>) {
    if let Err(error) = result
        && error.kind != ActionErrorKind::Operation
    {
        report_engine_action_failure(error);
    }
}
