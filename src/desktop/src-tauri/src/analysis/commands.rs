use crate::analysis::{AnalysisGetActiveResult, AnalysisRunProjection, AnalysisStartResult};
use crate::engine_protocol::{
    EngineActionError, await_action_task, report_rust_originated_failure,
};
use tauri::State;

use super::services::AnalysisState;

/// Starts an analysis run for a prepared comparison.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn analysis_start(
    state: State<'_, AnalysisState>,
    path: String,
    target: String,
    freshness_token: String,
    change_context: Option<String>,
) -> Result<AnalysisStartResult, EngineActionError> {
    let analysis_service = state.service();
    let result = await_action_task(move || {
        analysis_service.start(&path, &target, &freshness_token, change_context.as_deref())
    })
    .await;

    report_rust_originated_failure(&result);

    result
}

/// Looks up the active analysis run for a repository, if one exists.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn analysis_get_active(
    state: State<'_, AnalysisState>,
    path: String,
) -> Result<AnalysisGetActiveResult, EngineActionError> {
    let analysis_service = state.service();
    let result = await_action_task(move || analysis_service.get_active(&path)).await;

    report_rust_originated_failure(&result);

    result
}

/// Polls the current projection of one analysis run.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn analysis_poll_run(
    state: State<'_, AnalysisState>,
    run_id: String,
) -> Result<AnalysisRunProjection, EngineActionError> {
    let analysis_service = state.service();
    let result = await_action_task(move || analysis_service.poll_run(&run_id)).await;

    report_rust_originated_failure(&result);

    result
}

/// Requests cancellation of one analysis run.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn analysis_cancel(
    state: State<'_, AnalysisState>,
    run_id: String,
) -> Result<(), EngineActionError> {
    let analysis_service = state.service();
    let result = await_action_task(move || analysis_service.cancel(&run_id)).await;

    report_rust_originated_failure(&result);

    result
}
