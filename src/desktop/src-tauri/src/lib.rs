pub mod comparisons;
pub mod engine_protocol;
pub mod engine_status;
pub mod preferences;
pub mod repositories;

use comparisons::{
    ComparisonState, comparison_check_freshness, comparison_check_remote_baseline,
    comparison_list_targets, comparison_prepare, comparison_refresh_remote_baseline,
};
use engine_protocol::EngineClient;
use engine_status::{EngineStatusState, engine_check_status};
use preferences::{PreferenceState, preference_get_color_theme, preference_set_color_theme};
use repositories::{
    NativeRepositoryFolderPicker, RepositoryFolderPickerState, RepositoryState,
    repository_list_recent, repository_open, repository_remove_recent, repository_restore_last,
    select_repository_folder,
};
use std::sync::Arc;
use tauri::Manager;

const PRIMARY_WINDOW_LABEL: &str = "main";

/// Configures the desktop runtime with its explicit commands and injected services.
pub fn configure_desktop<R: tauri::Runtime>(
    builder: tauri::Builder<R>,
    engine_status_state: EngineStatusState,
    repository_state: RepositoryState,
    repository_folder_picker_state: RepositoryFolderPickerState,
    comparison_state: ComparisonState,
) -> tauri::Builder<R> {
    configure_desktop_with_preferences(
        builder,
        engine_status_state,
        repository_state,
        repository_folder_picker_state,
        comparison_state,
        PreferenceState::unused(),
    )
}

/// Configures the desktop runtime with explicit preference command state.
#[doc(hidden)]
pub fn configure_desktop_with_preferences<R: tauri::Runtime>(
    builder: tauri::Builder<R>,
    engine_status_state: EngineStatusState,
    repository_state: RepositoryState,
    repository_folder_picker_state: RepositoryFolderPickerState,
    comparison_state: ComparisonState,
    preference_state: PreferenceState,
) -> tauri::Builder<R> {
    builder
        .manage(engine_status_state)
        .manage(repository_state)
        .manage(repository_folder_picker_state)
        .manage(comparison_state)
        .manage(preference_state)
        .invoke_handler(tauri::generate_handler![
            engine_check_status,
            select_repository_folder,
            repository_open,
            repository_restore_last,
            repository_list_recent,
            repository_remove_recent,
            preference_get_color_theme,
            preference_set_color_theme,
            comparison_list_targets,
            comparison_prepare,
            comparison_check_freshness,
            comparison_check_remote_baseline,
            comparison_refresh_remote_baseline,
        ])
}

/// Handles desktop lifecycle events that affect the Engine process.
#[doc(hidden)]
pub fn handle_desktop_run_event(engine_client: &EngineClient, event: &tauri::RunEvent) {
    if matches!(event, tauri::RunEvent::ExitRequested { .. }) {
        engine_client.shutdown();
    }
}

fn focus_primary_window<R: tauri::Runtime>(app_handle: &tauri::AppHandle<R>) {
    let Some(window) = app_handle.get_webview_window(PRIMARY_WINDOW_LABEL) else {
        return;
    };

    let _ = window.unminimize();
    let _ = window.set_focus();
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let engine_client = Arc::new(EngineClient::new());

    let app = configure_desktop_with_preferences(
        tauri::Builder::default().plugin(tauri_plugin_single_instance::init(
            |app_handle, _arguments, _working_directory| {
                focus_primary_window(app_handle);
            },
        )),
        EngineStatusState::new(engine_client.clone()),
        RepositoryState::new(engine_client.clone()),
        RepositoryFolderPickerState::new(Arc::new(NativeRepositoryFolderPicker)),
        ComparisonState::new(engine_client.clone()),
        PreferenceState::new(engine_client.clone()),
    )
    .build(tauri::generate_context!())
    .expect("the ChangeLens desktop runtime could not be started");

    app.run(move |_app_handle, event| handle_desktop_run_event(&engine_client, &event));
}
