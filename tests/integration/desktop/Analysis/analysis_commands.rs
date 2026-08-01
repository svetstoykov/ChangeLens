use changelens_desktop_lib::analysis::{
    AnalysisComparison, AnalysisGetActiveResult, AnalysisRepository, AnalysisRunProjection,
    AnalysisRunState, AnalysisService, AnalysisStartResult, AnalysisState,
};
use changelens_desktop_lib::configure_desktop;
use changelens_desktop_lib::engine_protocol::{
    ActionErrorDetail, ActionErrorKind, EngineActionError, OperationErrorType,
};
use changelens_desktop_lib::engine_status::{EngineStatusService, EngineStatusState};
use changelens_desktop_lib::repositories::{
    RepositoryDescriptor, RepositoryFolderPicker, RepositoryFolderPickerState, RepositoryService,
    RepositoryState,
};
use raw_window_handle::HasWindowHandle;
use std::path::PathBuf;
use std::process::Command;
use std::sync::{Arc, Mutex};
use std::thread::ThreadId;
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

const REPOSITORY_PATH: &str = "/projects/change_lens";
const TARGET: &str = "refs/remotes/origin/main";
const FRESHNESS_TOKEN: &str = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
const RUN_ID: &str = "0198a1b2-3c4d-4e5f-8a9b-0123456789ab";
const DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE: &str = "CHANGELENS_ANALYSIS_COMMAND_DIAGNOSTIC_CHILD";

struct SuccessfulEngineStatusService;

impl EngineStatusService for SuccessfulEngineStatusService {
    fn check_status(&self) -> Result<(), EngineActionError> {
        Ok(())
    }
}

struct UnusedRepositoryFolderPicker;

impl RepositoryFolderPicker for UnusedRepositoryFolderPicker {
    fn select_folder(
        &self,
        _owner: &dyn HasWindowHandle,
    ) -> Result<Option<PathBuf>, EngineActionError> {
        unreachable!("the analysis command test does not select repository folders")
    }
}

struct UnusedRepositoryService;

impl RepositoryService for UnusedRepositoryService {
    fn open_repository(&self, _path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        unreachable!("the analysis command test does not open repositories")
    }
}

struct FixedAnalysisService {
    calls: Arc<Mutex<Vec<AnalysisCall>>>,
    start_result: Result<AnalysisStartResult, EngineActionError>,
    get_active_result: Result<AnalysisGetActiveResult, EngineActionError>,
    poll_result: Result<AnalysisRunProjection, EngineActionError>,
    cancel_result: Result<(), EngineActionError>,
}

impl AnalysisService for FixedAnalysisService {
    fn start(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        change_context: Option<&str>,
    ) -> Result<AnalysisStartResult, EngineActionError> {
        self.record(AnalysisCall::Start {
            path: path.to_owned(),
            target: target.to_owned(),
            freshness_token: freshness_token.to_owned(),
            change_context: change_context.map(str::to_owned),
            thread_id: std::thread::current().id(),
        });
        self.start_result.clone()
    }

    fn get_active(&self, path: &str) -> Result<AnalysisGetActiveResult, EngineActionError> {
        self.record(AnalysisCall::GetActive {
            path: path.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.get_active_result.clone()
    }

    fn poll_run(&self, run_id: &str) -> Result<AnalysisRunProjection, EngineActionError> {
        self.record(AnalysisCall::Poll {
            run_id: run_id.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.poll_result.clone()
    }

    fn cancel(&self, run_id: &str) -> Result<(), EngineActionError> {
        self.record(AnalysisCall::Cancel {
            run_id: run_id.to_owned(),
            thread_id: std::thread::current().id(),
        });
        self.cancel_result.clone()
    }
}

impl FixedAnalysisService {
    fn record(&self, call: AnalysisCall) {
        self.calls
            .lock()
            .expect("the recorded analysis calls should be available")
            .push(call);
    }
}

enum AnalysisCall {
    Start {
        path: String,
        target: String,
        freshness_token: String,
        change_context: Option<String>,
        thread_id: ThreadId,
    },
    GetActive {
        path: String,
        thread_id: ThreadId,
    },
    Poll {
        run_id: String,
        thread_id: ThreadId,
    },
    Cancel {
        run_id: String,
        thread_id: ThreadId,
    },
}

#[test]
fn analysis_start_forwards_exact_camel_case_arguments_and_success_shape() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "analysis_start",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
            "freshnessToken": FRESHNESS_TOKEN,
            "changeContext": null,
        })),
        analysis_service(Arc::clone(&calls), Ok(())),
    )
    .expect("the accepted analysis result should be returned");

    assert_eq!(
        response,
        serde_json::json!({
            "state": "accepted",
            "runId": RUN_ID,
            "requestedAt": 1_000,
        })
    );
    let calls = calls
        .lock()
        .expect("the recorded analysis calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [AnalysisCall::Start {
            path,
            target,
            freshness_token,
            change_context: None,
            ..
        }] if path == REPOSITORY_PATH && target == TARGET && freshness_token == FRESHNESS_TOKEN
    ));
}

#[test]
fn analysis_get_active_forwards_path_and_success_shape() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "analysis_get_active",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "path": REPOSITORY_PATH })),
        analysis_service(Arc::clone(&calls), Ok(())),
    )
    .expect("the active analysis result should be returned");

    assert_eq!(response, serde_json::json!({ "state": "none" }));
    let calls = calls
        .lock()
        .expect("the recorded analysis calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [AnalysisCall::GetActive { path, .. }] if path == REPOSITORY_PATH
    ));
}

#[test]
fn analysis_poll_run_forwards_run_id_and_success_shape() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "analysis_poll_run",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "runId": RUN_ID })),
        analysis_service(Arc::clone(&calls), Ok(())),
    )
    .expect("the analysis projection should be returned");

    assert_eq!(response["runId"], RUN_ID);
    assert_eq!(response["state"], "pendingCapture");
    let calls = calls
        .lock()
        .expect("the recorded analysis calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [AnalysisCall::Poll { run_id, .. }] if run_id == RUN_ID
    ));
}

#[test]
fn analysis_cancel_forwards_run_id_and_returns_null() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let response = invoke_command(
        "analysis_cancel",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "runId": RUN_ID })),
        analysis_service(Arc::clone(&calls), Ok(())),
    )
    .expect("the analysis cancellation should succeed");

    assert_eq!(response, serde_json::Value::Null);
    let calls = calls
        .lock()
        .expect("the recorded analysis calls should be available");
    assert!(matches!(
        calls.as_slice(),
        [AnalysisCall::Cancel { run_id, .. }] if run_id == RUN_ID
    ));
}

#[test]
fn analysis_commands_run_on_blocking_workers() {
    let calls = Arc::new(Mutex::new(Vec::new()));
    let calling_thread = std::thread::current().id();
    let service = analysis_service(Arc::clone(&calls), Ok(()));

    invoke_command(
        "analysis_start",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": REPOSITORY_PATH,
            "target": TARGET,
            "freshnessToken": FRESHNESS_TOKEN,
        })),
        Arc::clone(&service),
    )
    .expect("the analysis start command should be registered");
    invoke_command(
        "analysis_get_active",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "path": REPOSITORY_PATH })),
        Arc::clone(&service),
    )
    .expect("the active lookup command should be registered");
    invoke_command(
        "analysis_poll_run",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "runId": RUN_ID })),
        Arc::clone(&service),
    )
    .expect("the poll command should be registered");
    invoke_command(
        "analysis_cancel",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "runId": RUN_ID })),
        service,
    )
    .expect("the cancel command should be registered");

    let calls = calls
        .lock()
        .expect("the recorded analysis calls should be available");
    assert_eq!(calls.len(), 4);
    assert!(calls.iter().all(|call| call.thread_id() != calling_thread));
}

#[test]
fn configured_application_exposes_every_analysis_command_and_no_generic_action_command() {
    let service = analysis_service(Arc::new(Mutex::new(Vec::new())), Ok(()));

    let response = invoke_command(
        "engine_action",
        tauri::ipc::InvokeBody::Json(serde_json::json!({})),
        service,
    )
    .expect_err("the application must not expose a generic engine action command");

    assert!(response.to_string().contains("engine_action"));
}

#[test]
fn analysis_operation_errors_are_not_written_to_diagnostics() {
    let output = Command::new(
        std::env::current_exe().expect("the analysis command test executable should be available"),
    )
    .args([
        "--exact",
        "analysis_diagnostic_child_process",
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
    assert!(!standard_error.contains("analysis.unknownRun"));
    assert!(!standard_error.contains("engine.actionFailed"));
}

#[test]
fn analysis_diagnostic_child_process() {
    if std::env::var_os(DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE).is_none() {
        return;
    }

    let calls = Arc::new(Mutex::new(Vec::new()));
    invoke_command(
        "analysis_poll_run",
        tauri::ipc::InvokeBody::Json(serde_json::json!({ "runId": RUN_ID })),
        analysis_service(calls, Err(())),
    )
    .expect_err("the operation error should reach the analysis command boundary");
}

impl AnalysisCall {
    fn thread_id(&self) -> ThreadId {
        match self {
            Self::Start { thread_id, .. }
            | Self::GetActive { thread_id, .. }
            | Self::Poll { thread_id, .. }
            | Self::Cancel { thread_id, .. } => *thread_id,
        }
    }
}

fn invoke_command(
    command: &str,
    body: tauri::ipc::InvokeBody,
    analysis_service: Arc<dyn AnalysisService>,
) -> Result<serde_json::Value, serde_json::Value> {
    let app = configure_desktop(
        mock_builder(),
        EngineStatusState::new(Arc::new(SuccessfulEngineStatusService)),
        RepositoryState::new(Arc::new(UnusedRepositoryService)),
        RepositoryFolderPickerState::new(Arc::new(UnusedRepositoryFolderPicker)),
        changelens_desktop_lib::comparisons::ComparisonState::new(Arc::new(
            UnusedComparisonService,
        )),
        AnalysisState::new(analysis_service),
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

fn analysis_service(
    calls: Arc<Mutex<Vec<AnalysisCall>>>,
    error: Result<(), ()>,
) -> Arc<dyn AnalysisService> {
    let operation_error = error.err().map(|_| EngineActionError {
        kind: ActionErrorKind::Operation,
        request_id: Some("desktop-51".into()),
        errors: vec![ActionErrorDetail {
            error_type: OperationErrorType::NotFound,
            code: "analysis.unknownRun".into(),
            message: "No analysis run matches the supplied identifier.".into(),
        }],
    });
    let poll_result = match operation_error.clone() {
        Some(error) => Err(error),
        None => Ok(projection()),
    };
    let cancel_result = match operation_error {
        Some(error) => Err(error),
        None => Ok(()),
    };
    Arc::new(FixedAnalysisService {
        calls,
        start_result: Ok(AnalysisStartResult::Accepted {
            run_id: RUN_ID.into(),
            requested_at: 1_000,
        }),
        get_active_result: Ok(AnalysisGetActiveResult::None),
        poll_result,
        cancel_result,
    })
}

struct UnusedComparisonService;

impl changelens_desktop_lib::comparisons::ComparisonService for UnusedComparisonService {
    fn list_targets(
        &self,
        _path: &str,
        _query: Option<&str>,
        _after: Option<&str>,
        _target_set_token: Option<&str>,
    ) -> Result<changelens_desktop_lib::comparisons::ComparisonTargetPage, EngineActionError> {
        unreachable!("the analysis command test does not use comparison commands")
    }

    fn prepare(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<changelens_desktop_lib::comparisons::PreparedComparison, EngineActionError> {
        unreachable!("the analysis command test does not use comparison commands")
    }

    fn check_freshness(
        &self,
        _path: &str,
        _target: &str,
        _freshness_token: &str,
    ) -> Result<changelens_desktop_lib::comparisons::ComparisonFreshness, EngineActionError> {
        unreachable!("the analysis command test does not use comparison commands")
    }

    fn check_remote_baseline(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<changelens_desktop_lib::comparisons::ComparisonRemoteBaseline, EngineActionError>
    {
        unreachable!("the analysis command test does not use comparison commands")
    }

    fn refresh_remote_baseline(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<
        changelens_desktop_lib::comparisons::ComparisonRefreshRemoteBaselineResult,
        EngineActionError,
    > {
        unreachable!("the analysis command test does not use comparison commands")
    }
}

fn projection() -> AnalysisRunProjection {
    AnalysisRunProjection {
        run_id: RUN_ID.into(),
        state: AnalysisRunState::PendingCapture,
        repository: AnalysisRepository {
            repository_id: "5298a1b2-3c4d-4e5f-8a9b-0123456789ab".into(),
            display_name: "change_lens".into(),
            canonical_path: REPOSITORY_PATH.into(),
            head: "0123456789abcdef0123456789abcdef01234567".into(),
        },
        comparison: AnalysisComparison {
            target: TARGET.into(),
            target_revision: "89abcdef0123456789abcdef0123456789abcdef".into(),
            freshness_token: FRESHNESS_TOKEN.into(),
        },
        requested_at: 1_000,
        capture_started_at: None,
        captured_at: None,
        snapshot_id: None,
        cancellation_requested: false,
        facts: Vec::new(),
        terminal: None,
        interrupted_at: None,
        interruption_reason: None,
    }
}
