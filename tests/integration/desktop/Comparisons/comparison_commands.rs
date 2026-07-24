use changelens_desktop_lib::comparisons::{
    ComparisonFreshness, ComparisonReadiness, ComparisonService, ComparisonState, ComparisonTarget,
    ComparisonTargetKind, ComparisonTargetPage, PreparedComparison,
};
use changelens_desktop_lib::configure_desktop;
use changelens_desktop_lib::engine_protocol::{
    ActionErrorDetail, ActionErrorKind, EngineActionError, OperationErrorType,
};
use changelens_desktop_lib::engine_status::{EngineStatusService, EngineStatusState};
use changelens_desktop_lib::repositories::{
    RepositoryDescriptor, RepositoryFolderPicker, RepositoryFolderPickerState, RepositoryHead,
    RepositoryService, RepositoryState,
};
use std::path::PathBuf;
use std::process::Command;
use std::sync::{Arc, Mutex};
use std::thread::ThreadId;
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

const REPOSITORY_PATH: &str = "/projects/change_lens";
const TARGET: &str = "refs/remotes/origin/main";
const TARGET_SET_TOKEN: &str = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
const FRESHNESS_TOKEN: &str = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
const DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE: &str =
    "CHANGELENS_COMPARISON_COMMAND_DIAGNOSTIC_CHILD";
const PANIC_DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE: &str =
    "CHANGELENS_COMPARISON_COMMAND_PANIC_DIAGNOSTIC_CHILD";

struct SuccessfulEngineStatusService;

impl EngineStatusService for SuccessfulEngineStatusService {
    fn check_status(&self) -> Result<(), EngineActionError> {
        Ok(())
    }
}

struct UnusedRepositoryFolderPicker;

impl RepositoryFolderPicker for UnusedRepositoryFolderPicker {
    fn select_folder(&self) -> Result<Option<PathBuf>, EngineActionError> {
        unreachable!("the comparison command test does not select repository folders")
    }
}

struct UnusedRepositoryService;

impl RepositoryService for UnusedRepositoryService {
    fn open_repository(&self, _path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        unreachable!("the comparison command test does not open repositories")
    }
}

struct FixedComparisonService {
    calls: Arc<Mutex<Vec<ComparisonCall>>>,
    list_result: Result<ComparisonTargetPage, EngineActionError>,
    prepare_result: Result<PreparedComparison, EngineActionError>,
    freshness_result: Result<ComparisonFreshness, EngineActionError>,
    panic_on_call: bool,
}

struct BlockingRuntimeProbeComparisonService {
    calls: Arc<Mutex<Vec<ComparisonCall>>>,
}

impl ComparisonService for BlockingRuntimeProbeComparisonService {
    fn list_targets(
        &self,
        path: &str,
        query: Option<&str>,
        after: Option<&str>,
        target_set_token: Option<&str>,
    ) -> Result<ComparisonTargetPage, EngineActionError> {
        self.record(ComparisonCall::ListTargets {
            path: path.to_owned(),
            query: query.map(str::to_owned),
            after: after.map(str::to_owned),
            target_set_token: target_set_token.map(str::to_owned),
            thread_id: std::thread::current().id(),
        });
        self.verify_blocking_worker();

        Ok(target_page())
    }

    fn prepare(&self, path: &str, target: &str) -> Result<PreparedComparison, EngineActionError> {
        self.record(ComparisonCall::Prepare {
            path: path.to_owned(),
            target: target.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.verify_blocking_worker();

        Ok(prepared_comparison())
    }

    fn check_freshness(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        self.record(ComparisonCall::CheckFreshness {
            path: path.to_owned(),
            target: target.to_owned(),
            freshness_token: freshness_token.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.verify_blocking_worker();

        Ok(ComparisonFreshness::Current)
    }
}

impl BlockingRuntimeProbeComparisonService {
    fn record(&self, call: ComparisonCall) {
        self.calls
            .lock()
            .expect("the recorded comparison calls should be available")
            .push(call);
    }

    fn verify_blocking_worker(&self) {
        tauri::async_runtime::block_on(std::future::ready(()));
    }
}

impl ComparisonService for FixedComparisonService {
    fn list_targets(
        &self,
        path: &str,
        query: Option<&str>,
        after: Option<&str>,
        target_set_token: Option<&str>,
    ) -> Result<ComparisonTargetPage, EngineActionError> {
        self.record(ComparisonCall::ListTargets {
            path: path.to_owned(),
            query: query.map(str::to_owned),
            after: after.map(str::to_owned),
            target_set_token: target_set_token.map(str::to_owned),
            thread_id: std::thread::current().id(),
        });
        self.panic_if_requested();
        self.list_result.clone()
    }

    fn prepare(&self, path: &str, target: &str) -> Result<PreparedComparison, EngineActionError> {
        self.record(ComparisonCall::Prepare {
            path: path.to_owned(),
            target: target.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.panic_if_requested();
        self.prepare_result.clone()
    }

    fn check_freshness(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        self.record(ComparisonCall::CheckFreshness {
            path: path.to_owned(),
            target: target.to_owned(),
            freshness_token: freshness_token.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.panic_if_requested();
        self.freshness_result.clone()
    }
}

impl FixedComparisonService {
    fn record(&self, call: ComparisonCall) {
        self.calls
            .lock()
            .expect("the recorded comparison calls should be available")
            .push(call);
    }

    fn panic_if_requested(&self) {
        assert!(!self.panic_on_call, "comparison service fixture panic");
    }
}

enum ComparisonCall {
    ListTargets {
        path: String,
        query: Option<String>,
        after: Option<String>,
        target_set_token: Option<String>,
        thread_id: ThreadId,
    },
    Prepare {
        path: String,
        target: String,
        thread_id: ThreadId,
    },
    CheckFreshness {
        path: String,
        target: String,
        freshness_token: String,
        thread_id: ThreadId,
    },
}

#[test]
fn comparison_list_targets_forwards_exact_camel_case_arguments_and_success_shape() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "comparison_list_targets",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "query": "main",
            "after": "refs/heads/release",
            "targetSetToken": TARGET_SET_TOKEN,
        })),
        comparison_service(Arc::clone(&calls), false),
    )
    .expect("the target page should be returned");

    assert_eq!(
        response,
        serde_json::json!({
            "targets": [target_json()],
            "suggestedTarget": target_json(),
            "nextCursor": null,
            "targetSetToken": TARGET_SET_TOKEN,
            "unsupportedTargetCount": 2,
        })
    );

    let calls = calls
        .lock()
        .expect("the recorded comparison calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [ComparisonCall::ListTargets {
            path,
            query: Some(query),
            after: Some(after),
            target_set_token: Some(token),
            ..
        }] if path == REPOSITORY_PATH
            && query == "main"
            && after == "refs/heads/release"
            && token == TARGET_SET_TOKEN
    ));
}

#[test]
fn comparison_list_targets_keeps_omitted_optional_arguments_as_none() {
    let calls = Arc::new(Mutex::new(Vec::new()));

    invoke_command(
        "comparison_list_targets",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "path": REPOSITORY_PATH })),
        comparison_service(Arc::clone(&calls), false),
    )
    .expect("the target page should be returned when optional arguments are omitted");

    let calls = calls
        .lock()
        .expect("the recorded comparison calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [ComparisonCall::ListTargets {
            path,
            query: None,
            after: None,
            target_set_token: None,
            ..
        }] if path == REPOSITORY_PATH
    ));
}

#[test]
fn comparison_prepare_forwards_exact_camel_case_arguments_and_success_shape() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        comparison_service(Arc::clone(&calls), false),
    )
    .expect("the prepared comparison should be returned");

    assert_eq!(response, prepared_comparison_json());

    let calls = calls
        .lock()
        .expect("the recorded comparison calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [ComparisonCall::Prepare {
            path,
            target,
            ..
        }] if path == REPOSITORY_PATH && target == TARGET
    ));
}

#[test]
fn comparison_check_freshness_forwards_exact_camel_case_arguments_and_success_shape() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "comparison_check_freshness",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
            "freshnessToken": FRESHNESS_TOKEN,
        })),
        comparison_service(Arc::clone(&calls), false),
    )
    .expect("the comparison freshness should be returned");

    assert_eq!(response, serde_json::json!({ "state": "current" }));

    let calls = calls
        .lock()
        .expect("the recorded comparison calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [ComparisonCall::CheckFreshness {
            path,
            target,
            freshness_token,
            ..
        }] if path == REPOSITORY_PATH && target == TARGET && freshness_token == FRESHNESS_TOKEN
    ));
}

#[test]
fn comparison_prepare_preserves_ordered_operation_errors_without_relogging() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        fixed_comparison_service(
            Arc::clone(&calls),
            Ok(target_page()),
            Err(ordered_operation_error()),
            Ok(ComparisonFreshness::Current),
            false,
        ),
    )
    .expect_err("Engine operation errors should reject the command");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "operation",
            "requestId": "desktop-51",
            "errors": [
                {
                    "type": "Validation",
                    "code": "fixture.first",
                    "message": "The first fixture value is invalid.",
                },
                {
                    "type": "Conflict",
                    "code": "fixture.second",
                    "message": "The second fixture value conflicts with current state.",
                },
            ],
        })
    );
}

#[test]
fn comparison_task_panic_returns_one_sanitized_unexpected_error() {
    let response = invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        comparison_service(Arc::new(Mutex::new(Vec::new())), true),
    )
    .expect_err("a comparison task panic should reject the command");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "unexpected",
            "errors": [{
                "type": "InternalError",
                "code": "desktop.actionTaskFailed",
                "message": "The desktop could not complete the engine action task.",
            }],
        })
    );
}

#[test]
fn comparison_task_panic_logs_one_sanitized_diagnostic_without_raw_payload() {
    let output = Command::new(
        std::env::current_exe()
            .expect("the comparison command test executable should be available"),
    )
    .args([
        "--exact",
        "comparison_panic_diagnostic_child_process",
        "--nocapture",
        "--test-threads=1",
    ])
    .env(PANIC_DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE, "1")
    .output()
    .expect("the panic diagnostic child process should run");

    assert!(
        output.status.success(),
        "the panic diagnostic child process should pass: {}",
        String::from_utf8_lossy(&output.stderr)
    );

    let standard_error = String::from_utf8(output.stderr)
        .expect("the panic diagnostic child process should write UTF-8 diagnostics");
    assert_eq!(
        standard_error
            .matches("\"event\":\"engine.actionFailed\"")
            .count(),
        1
    );
    assert_eq!(
        standard_error.matches("desktop.actionTaskFailed").count(),
        1
    );
    assert!(!standard_error.contains("comparison service fixture panic"));
}

#[test]
fn comparison_commands_run_on_blocking_workers() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let calling_thread = std::thread::current().id();

    invoke_command(
        "comparison_list_targets",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "path": REPOSITORY_PATH })),
        blocking_runtime_probe_comparison_service(Arc::clone(&calls)),
    )
    .expect("the target page should be returned");
    invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        blocking_runtime_probe_comparison_service(Arc::clone(&calls)),
    )
    .expect("the prepared comparison should be returned");
    invoke_command(
        "comparison_check_freshness",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
            "freshnessToken": FRESHNESS_TOKEN,
        })),
        blocking_runtime_probe_comparison_service(Arc::clone(&calls)),
    )
    .expect("the comparison freshness should be returned");

    let calls = calls
        .lock()
        .expect("the recorded comparison calls should be available");
    assert_eq!(calls.len(), 3);
    assert!(calls.iter().all(|call| call.thread_id() != calling_thread));
}

#[test]
fn configured_application_exposes_only_fixed_comparison_commands() {
    let service = comparison_service(Arc::new(Mutex::new(Vec::new())), false);

    invoke_command(
        "comparison_list_targets",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "path": REPOSITORY_PATH })),
        Arc::clone(&service),
    )
    .expect("the list-targets command must be registered");
    invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        Arc::clone(&service),
    )
    .expect("the prepare command must be registered");
    invoke_command(
        "comparison_check_freshness",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
            "freshnessToken": FRESHNESS_TOKEN,
        })),
        Arc::clone(&service),
    )
    .expect("the freshness command must be registered");

    let response = invoke_command(
        "engine_action",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "action": "comparisons.prepare",
            "parameters": { "path": REPOSITORY_PATH, "target": TARGET },
        })),
        service,
    )
    .expect_err("the application must not expose a generic engine action command");

    assert!(response.to_string().contains("engine_action"));
}

#[test]
fn comparison_operation_errors_are_not_written_to_diagnostics() {
    let output = Command::new(
        std::env::current_exe()
            .expect("the comparison command test executable should be available"),
    )
    .args([
        "--exact",
        "comparison_diagnostic_child_process",
        "--nocapture",
        "--test-threads=1",
    ])
    .env(DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE, "1")
    .output()
    .expect("the diagnostic child process should run");

    assert!(
        output.status.success(),
        "the diagnostic child process should pass: {}",
        String::from_utf8_lossy(&output.stderr)
    );

    let standard_error = String::from_utf8(output.stderr)
        .expect("the diagnostic child process should write UTF-8 diagnostics");
    assert!(!standard_error.contains("fixture.first"));
    assert!(!standard_error.contains("fixture.second"));
    assert!(!standard_error.contains("engine.actionFailed"));
}

#[test]
fn comparison_diagnostic_child_process() {
    if std::env::var_os(DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE).is_none() {
        return;
    }

    invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        fixed_comparison_service(
            Arc::new(Mutex::new(Vec::new())),
            Ok(target_page()),
            Err(ordered_operation_error()),
            Ok(ComparisonFreshness::Current),
            false,
        ),
    )
    .expect_err("the operation error should reach the comparison command boundary");
}

#[test]
fn comparison_panic_diagnostic_child_process() {
    if std::env::var_os(PANIC_DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE).is_none() {
        return;
    }

    invoke_command(
        "comparison_prepare",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
        })),
        comparison_service(Arc::new(Mutex::new(Vec::new())), true),
    )
    .expect_err("the panicking task should reach the command boundary");
}

impl ComparisonCall {
    fn thread_id(&self) -> ThreadId {
        match self {
            Self::ListTargets { thread_id, .. }
            | Self::Prepare { thread_id, .. }
            | Self::CheckFreshness { thread_id, .. } => *thread_id,
        }
    }
}

fn invoke_command(
    command: &str,
    body: tauri::ipc::InvokeBody,
    comparison_service: Arc<dyn ComparisonService>,
) -> Result<serde_json::Value, serde_json::Value> {
    let app = configure_desktop(
        mock_builder(),
        EngineStatusState::new(Arc::new(SuccessfulEngineStatusService)),
        RepositoryState::new(Arc::new(UnusedRepositoryService)),
        RepositoryFolderPickerState::new(Arc::new(UnusedRepositoryFolderPicker)),
        ComparisonState::new(comparison_service),
    )
    .build(mock_context(noop_assets()))
    .expect("the test desktop application should build");
    let webview = tauri::WebviewWindowBuilder::new(&app, "main", Default::default())
        .build()
        .expect("the test webview should build");
    let request = tauri::webview::InvokeRequest {
        cmd: command.into(),
        callback: tauri::ipc::CallbackFn(0),
        error: tauri::ipc::CallbackFn(1),
        url: if cfg!(any(windows, target_os = "android")) {
            "http://tauri.localhost"
        } else {
            "tauri://localhost"
        }
        .parse()
        .expect("the test IPC URL should be valid"),
        body,
        headers: Default::default(),
        invoke_key: INVOKE_KEY.to_string(),
    };

    get_ipc_response(&webview, request)
        .map(|body| body.deserialize().expect("the success body should be JSON"))
}

fn comparison_service(
    calls: Arc<Mutex<Vec<ComparisonCall>>>,
    panic_on_call: bool,
) -> Arc<dyn ComparisonService> {
    fixed_comparison_service(
        calls,
        Ok(target_page()),
        Ok(prepared_comparison()),
        Ok(ComparisonFreshness::Current),
        panic_on_call,
    )
}

fn blocking_runtime_probe_comparison_service(
    calls: Arc<Mutex<Vec<ComparisonCall>>>,
) -> Arc<dyn ComparisonService> {
    Arc::new(BlockingRuntimeProbeComparisonService { calls })
}

fn fixed_comparison_service(
    calls: Arc<Mutex<Vec<ComparisonCall>>>,
    list_result: Result<ComparisonTargetPage, EngineActionError>,
    prepare_result: Result<PreparedComparison, EngineActionError>,
    freshness_result: Result<ComparisonFreshness, EngineActionError>,
    panic_on_call: bool,
) -> Arc<dyn ComparisonService> {
    Arc::new(FixedComparisonService {
        calls,
        list_result,
        prepare_result,
        freshness_result,
        panic_on_call,
    })
}

fn ordered_operation_error() -> EngineActionError {
    EngineActionError {
        kind: ActionErrorKind::Operation,
        request_id: Some("desktop-51".into()),
        errors: vec![
            ActionErrorDetail {
                error_type: OperationErrorType::Validation,
                code: "fixture.first".into(),
                message: "The first fixture value is invalid.".into(),
            },
            ActionErrorDetail {
                error_type: OperationErrorType::Conflict,
                code: "fixture.second".into(),
                message: "The second fixture value conflicts with current state.".into(),
            },
        ],
    }
}

fn target() -> ComparisonTarget {
    ComparisonTarget {
        kind: ComparisonTargetKind::RemoteTracking,
        name: "origin/main".into(),
        full_name: TARGET.into(),
        revision: "0123456789abcdef0123456789abcdef01234567".into(),
    }
}

fn target_page() -> ComparisonTargetPage {
    ComparisonTargetPage {
        targets: vec![target()],
        suggested_target: Some(target()),
        next_cursor: None,
        target_set_token: TARGET_SET_TOKEN.into(),
        unsupported_target_count: 2,
    }
}

fn prepared_comparison() -> PreparedComparison {
    PreparedComparison {
        repository: RepositoryDescriptor {
            name: "change_lens".into(),
            canonical_path: REPOSITORY_PATH.into(),
            head: RepositoryHead::Branch {
                name: "main".into(),
                revision: "0123456789abcdef0123456789abcdef01234567".into(),
            },
        },
        target: target(),
        merge_base_revision: "abcdef0123456789abcdef0123456789abcdef0123".into(),
        current_work_commit_count: 3,
        target_only_commit_count: 2,
        changed_file_total: 7,
        uncommitted_file_total: 1,
        staged_file_count: 1,
        unstaged_file_count: 0,
        untracked_file_count: 0,
        readiness: ComparisonReadiness::Ready,
        freshness_token: FRESHNESS_TOKEN.into(),
    }
}

fn target_json() -> serde_json::Value {
    serde_json::json!({
        "kind": "remoteTracking",
        "name": "origin/main",
        "fullName": TARGET,
        "revision": "0123456789abcdef0123456789abcdef01234567",
    })
}

fn prepared_comparison_json() -> serde_json::Value {
    serde_json::json!({
        "repository": {
            "name": "change_lens",
            "canonicalPath": REPOSITORY_PATH,
            "head": {
                "kind": "branch",
                "name": "main",
                "revision": "0123456789abcdef0123456789abcdef01234567",
            },
        },
        "target": target_json(),
        "mergeBaseRevision": "abcdef0123456789abcdef0123456789abcdef0123",
        "currentWorkCommitCount": 3,
        "targetOnlyCommitCount": 2,
        "changedFileTotal": 7,
        "uncommittedFileTotal": 1,
        "stagedFileCount": 1,
        "unstagedFileCount": 0,
        "untrackedFileCount": 0,
        "readiness": { "state": "ready" },
        "freshnessToken": FRESHNESS_TOKEN,
    })
}
