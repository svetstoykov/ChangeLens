use crate::engine_protocol::{
    EngineActionError, await_action_task, report_rust_originated_failure,
};
use crate::preferences::{ColorTheme, ColorThemePreference, PreferenceState};
use tauri::State;

#[tauri::command]
pub(crate) async fn preference_get_color_theme(
    state: State<'_, PreferenceState>,
) -> Result<ColorThemePreference, EngineActionError> {
    let service = state.service();
    let result = await_action_task(move || service.get_color_theme()).await;
    report_rust_originated_failure(&result);
    result
}

#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn preference_set_color_theme(
    state: State<'_, PreferenceState>,
    color_theme: ColorTheme,
) -> Result<(), EngineActionError> {
    let service = state.service();
    let result = await_action_task(move || service.set_color_theme(color_theme)).await;
    report_rust_originated_failure(&result);
    result
}
