use crate::comparisons::{
    ComparisonFreshness, ComparisonState, ComparisonTargetPage, PreparedComparison,
};
use crate::engine_protocol::{
    EngineActionError, action_task_failed, report_rust_originated_failure,
};
use tauri::State;

/// Lists the bounded comparison targets available for a repository.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn comparison_list_targets(
    state: State<'_, ComparisonState>,
    path: String,
    query: Option<String>,
    after: Option<String>,
    target_set_token: Option<String>,
) -> Result<ComparisonTargetPage, EngineActionError> {
    let comparison_service = state.service();
    let result = match tauri::async_runtime::spawn_blocking(move || {
        comparison_service.list_targets(
            &path,
            query.as_deref(),
            after.as_deref(),
            target_set_token.as_deref(),
        )
    })
    .await
    {
        Ok(result) => result,
        Err(_) => Err(action_task_failed()),
    };

    report_rust_originated_failure(&result);

    result
}

/// Prepares the exact comparison against a selected target.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn comparison_prepare(
    state: State<'_, ComparisonState>,
    path: String,
    target: String,
) -> Result<PreparedComparison, EngineActionError> {
    let comparison_service = state.service();
    let result = match tauri::async_runtime::spawn_blocking(move || {
        comparison_service.prepare(&path, &target)
    })
    .await
    {
        Ok(result) => result,
        Err(_) => Err(action_task_failed()),
    };

    report_rust_originated_failure(&result);

    result
}

/// Checks whether an already prepared comparison remains current.
#[tauri::command(rename_all = "camelCase")]
pub(crate) async fn comparison_check_freshness(
    state: State<'_, ComparisonState>,
    path: String,
    target: String,
    freshness_token: String,
) -> Result<ComparisonFreshness, EngineActionError> {
    let comparison_service = state.service();
    let result = match tauri::async_runtime::spawn_blocking(move || {
        comparison_service.check_freshness(&path, &target, &freshness_token)
    })
    .await
    {
        Ok(result) => result,
        Err(_) => Err(action_task_failed()),
    };

    report_rust_originated_failure(&result);

    result
}
