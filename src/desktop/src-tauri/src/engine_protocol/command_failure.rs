use crate::engine_protocol::{ActionErrorKind, EngineActionError, report_engine_action_failure};
use std::cell::Cell;
use std::panic::{self, AssertUnwindSafe};
use std::sync::Once;

thread_local! {
    static ACTION_TASK_PANIC_SUPPRESSION_ACTIVE: Cell<bool> = const { Cell::new(false) };
}

static ACTION_TASK_PANIC_HOOK: Once = Once::new();

/// Creates the sanitized error returned when a desktop command task cannot complete.
pub(crate) fn action_task_failed() -> EngineActionError {
    EngineActionError::unexpected(
        None,
        "desktop.actionTaskFailed",
        "The desktop could not complete the engine action task.",
    )
}

/// Runs a blocking desktop action task and converts a task panic into a sanitized error.
pub(crate) async fn await_action_task<T>(
    action_task: impl FnOnce() -> Result<T, EngineActionError> + Send + 'static,
) -> Result<T, EngineActionError>
where
    T: Send + 'static,
{
    install_action_task_panic_hook();

    match tauri::async_runtime::spawn_blocking(move || {
        ACTION_TASK_PANIC_SUPPRESSION_ACTIVE.with(|active| active.set(true));
        let result = panic::catch_unwind(AssertUnwindSafe(action_task));
        ACTION_TASK_PANIC_SUPPRESSION_ACTIVE.with(|active| active.set(false));

        result.unwrap_or_else(|_| Err(action_task_failed()))
    })
    .await
    {
        Ok(result) => result,
        Err(_) => Err(action_task_failed()),
    }
}

/// Reports desktop-originated failures while preserving Engine operation errors without relogging.
pub(crate) fn report_rust_originated_failure<T>(result: &Result<T, EngineActionError>) {
    if let Err(error) = result
        && error.kind != ActionErrorKind::Operation
    {
        report_engine_action_failure(error);
    }
}

fn install_action_task_panic_hook() {
    ACTION_TASK_PANIC_HOOK.call_once(|| {
        let previous_hook = panic::take_hook();

        panic::set_hook(Box::new(move |panic_info| {
            let suppress_diagnostic = ACTION_TASK_PANIC_SUPPRESSION_ACTIVE
                .try_with(|active| active.get())
                .unwrap_or(false);

            if !suppress_diagnostic {
                previous_hook(panic_info);
            }
        }));
    });
}
