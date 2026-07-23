pub mod engine_protocol;
pub mod engine_status;
pub mod repositories;

use engine_protocol::EngineClient;
use engine_status::{EngineStatusState, engine_check_status};
use repositories::{
    NativeRepositoryFolderPicker, RepositoryFolderPickerState, RepositoryState, repository_open,
    select_repository_folder,
};
use std::sync::Arc;

/// Configures the desktop runtime with its explicit commands and injected services.
pub fn configure_desktop<R: tauri::Runtime>(
    builder: tauri::Builder<R>,
    engine_status_state: EngineStatusState,
    repository_state: RepositoryState,
    repository_folder_picker_state: RepositoryFolderPickerState,
) -> tauri::Builder<R> {
    builder
        .manage(engine_status_state)
        .manage(repository_state)
        .manage(repository_folder_picker_state)
        .invoke_handler(tauri::generate_handler![
            engine_check_status,
            select_repository_folder,
            repository_open,
        ])
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let engine_client = Arc::new(EngineClient::new());

    let app = configure_desktop(
        tauri::Builder::default(),
        EngineStatusState::new(engine_client.clone()),
        RepositoryState::new(engine_client.clone()),
        RepositoryFolderPickerState::new(Arc::new(NativeRepositoryFolderPicker)),
    )
    .build(tauri::generate_context!())
    .expect("the ChangeLens desktop runtime could not be started");

    app.run(move |_app_handle, event| {
        if matches!(event, tauri::RunEvent::ExitRequested { .. }) {
            engine_client.shutdown();
        }
    });
}
