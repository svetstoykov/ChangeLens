use changelens_desktop_lib::analysis::AnalysisState;
use changelens_desktop_lib::comparisons::{
    ComparisonFreshness, ComparisonRefreshRemoteBaselineResult, ComparisonRemoteBaseline,
    ComparisonService, ComparisonState, ComparisonTargetPage, PreparedComparison,
};
use changelens_desktop_lib::engine_protocol::{
    ActionErrorDetail, ActionErrorKind, EngineActionError, EngineClient, OperationErrorType,
};
use changelens_desktop_lib::engine_status::{EngineStatusService, EngineStatusState};
use changelens_desktop_lib::preferences::{
    ColorTheme, ColorThemePreference, ColorThemePreferenceService, PreferenceState,
};
use changelens_desktop_lib::repositories::{
    RecentRepository, RepositoryDescriptor, RepositoryFolderPicker, RepositoryFolderPickerState,
    RepositoryHead, RepositoryHistory, RepositoryRestoreResult, RepositoryService, RepositoryState,
};
use changelens_desktop_lib::{
    configure_desktop, configure_desktop_with_preferences, handle_desktop_run_event,
};
use raw_window_handle::HasWindowHandle;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};
use tauri::Manager;
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

const SHA1_REVISION: &str = "0123456789abcdef0123456789abcdef01234567";
const DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE: &str =
    "CHANGELENS_REPOSITORY_COMMAND_DIAGNOSTIC_CHILD";
const REPOSITORY_PATH: &str = "/projects/change_lens";
const COMPARISON_TARGET: &str = "refs/remotes/origin/main";
const COMPARISON_FRESHNESS_TOKEN: &str =
    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

struct SuccessfulEngineStatusService;

impl EngineStatusService for SuccessfulEngineStatusService {
    fn check_status(&self) -> Result<(), EngineActionError> {
        Ok(())
    }
}

struct FixedRepositoryFolderPicker {
    result: Result<Option<PathBuf>, EngineActionError>,
}

struct WindowOwnedRepositoryFolderPicker {
    received_window_owner: Arc<AtomicBool>,
}

impl RepositoryFolderPicker for FixedRepositoryFolderPicker {
    fn select_folder(
        &self,
        _owner: &dyn HasWindowHandle,
    ) -> Result<Option<PathBuf>, EngineActionError> {
        self.result.clone()
    }
}

impl RepositoryFolderPicker for WindowOwnedRepositoryFolderPicker {
    fn select_folder(
        &self,
        owner: &dyn HasWindowHandle,
    ) -> Result<Option<PathBuf>, EngineActionError> {
        self.received_window_owner
            .store(owner.window_handle().is_ok(), Ordering::SeqCst);

        Ok(None)
    }
}

struct FixedRepositoryService {
    paths: Arc<Mutex<Vec<String>>>,
    result: Result<RepositoryDescriptor, EngineActionError>,
    panic_on_open: bool,
}

struct LocalStateRepositoryService {
    removed_ids: Arc<Mutex<Vec<String>>>,
}

struct FixedColorThemePreferenceService {
    set_themes: Arc<Mutex<Vec<ColorTheme>>>,
}

impl ColorThemePreferenceService for FixedColorThemePreferenceService {
    fn get_color_theme(&self) -> Result<ColorThemePreference, EngineActionError> {
        Ok(ColorThemePreference {
            color_theme: Some(ColorTheme::Dark),
        })
    }

    fn set_color_theme(&self, color_theme: ColorTheme) -> Result<(), EngineActionError> {
        self.set_themes
            .lock()
            .expect("the recorded color themes should be available")
            .push(color_theme);
        Ok(())
    }
}

impl RepositoryService for LocalStateRepositoryService {
    fn open_repository(&self, _path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        unreachable!("the local-state command fixture does not open repositories")
    }

    fn restore_last_repository(&self) -> Result<RepositoryRestoreResult, EngineActionError> {
        Ok(RepositoryRestoreResult::None)
    }

    fn list_recent_repositories(&self) -> Result<RepositoryHistory, EngineActionError> {
        Ok(RepositoryHistory {
            last_repository_id: Some("01234567-89ab-cdef-0123-456789abcdef".into()),
            repositories: vec![RecentRepository {
                repository_id: "01234567-89ab-cdef-0123-456789abcdef".into(),
                name: "change_lens".into(),
                canonical_path: REPOSITORY_PATH.into(),
                last_opened_at_unix_milliseconds: 1_785_081_600_000,
                preferred_target: Some(COMPARISON_TARGET.into()),
            }],
        })
    }

    fn remove_recent_repository(&self, repository_id: &str) -> Result<(), EngineActionError> {
        self.removed_ids
            .lock()
            .expect("the removed repository identifiers should be available")
            .push(repository_id.to_owned());
        Ok(())
    }
}

struct UnusedComparisonService;

impl ComparisonService for UnusedComparisonService {
    fn list_targets(
        &self,
        _path: &str,
        _query: Option<&str>,
        _after: Option<&str>,
        _target_set_token: Option<&str>,
    ) -> Result<ComparisonTargetPage, EngineActionError> {
        unreachable!("the repository command test does not list comparison targets")
    }

    fn prepare(&self, _path: &str, _target: &str) -> Result<PreparedComparison, EngineActionError> {
        unreachable!("the repository command test does not prepare comparisons")
    }

    fn check_freshness(
        &self,
        _path: &str,
        _target: &str,
        _freshness_token: &str,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        unreachable!("the repository command test does not check comparison freshness")
    }

    fn check_remote_baseline(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<ComparisonRemoteBaseline, EngineActionError> {
        unreachable!("the repository command test does not check the remote baseline")
    }

    fn refresh_remote_baseline(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<ComparisonRefreshRemoteBaselineResult, EngineActionError> {
        unreachable!("the repository command test does not refresh the remote baseline")
    }
}

impl RepositoryService for FixedRepositoryService {
    fn open_repository(&self, path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        self.paths
            .lock()
            .expect("the recorded repository paths should be available")
            .push(path.to_owned());

        assert!(!self.panic_on_open, "repository service fixture panic");

        self.result.clone()
    }
}

#[test]
fn picker_selection_serializes_a_unicode_path_as_a_string() {
    let selected_path = PathBuf::from("/tmp/Проекти/change_lens");

    let response = invoke_command(
        "select_repository_folder",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Ok(Some(selected_path))),
        repository_returning(Ok(branch_repository())),
    )
    .expect("a selected Unicode path should be returned");

    assert_eq!(response, serde_json::json!("/tmp/Проекти/change_lens"));
}

#[test]
fn picker_cancellation_serializes_as_successful_null() {
    let response = invoke_command(
        "select_repository_folder",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Ok(None)),
        repository_returning(Ok(branch_repository())),
    )
    .expect("picker cancellation should be a successful command result");

    assert_eq!(response, serde_json::Value::Null);
}

#[test]
fn picker_receives_the_invoking_window_as_its_owner() {
    let received_window_owner = Arc::new(AtomicBool::new(false));

    let response = invoke_command(
        "select_repository_folder",
        tauri::ipc::InvokeBody::default(),
        Arc::new(WindowOwnedRepositoryFolderPicker {
            received_window_owner: Arc::clone(&received_window_owner),
        }),
        repository_returning(Ok(branch_repository())),
    )
    .expect("the picker command should complete");

    assert_eq!(response, serde_json::Value::Null);
    assert!(
        received_window_owner.load(Ordering::SeqCst),
        "the native folder picker should receive the window that invoked the command"
    );
}

#[test]
fn picker_failure_returns_the_transport_error_unchanged() {
    let response = invoke_command(
        "select_repository_folder",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Err(folder_picker_unavailable())),
        repository_returning(Ok(branch_repository())),
    )
    .expect_err("a picker failure should reject the command");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "transport",
            "errors": [{
                "type": "ExternalDependencyFailure",
                "code": "desktop.folderPickerUnavailable",
                "message": "The desktop folder picker is unavailable.",
            }],
        })
    );
}

#[cfg(unix)]
#[test]
fn picker_rejects_a_non_unicode_path_without_lossy_conversion() {
    use std::ffi::OsString;
    use std::os::unix::ffi::OsStringExt;

    let selected_path = PathBuf::from(OsString::from_vec(vec![
        b'/', b't', b'm', b'p', b'/', b'r', b'e', b'p', b'o', 0xff,
    ]));

    let response = invoke_command(
        "select_repository_folder",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Ok(Some(selected_path))),
        repository_returning(Ok(branch_repository())),
    )
    .expect_err("a non-Unicode selected path should reject the command");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "transport",
            "errors": [{
                "type": "UnprocessableInput",
                "code": "repository.pathEncodingUnsupported",
                "message": "The selected path cannot be represented as Unicode.",
            }],
        })
    );
    assert!(!response.to_string().contains('\u{fffd}'));
}

#[test]
fn repository_open_forwards_the_exact_path_and_returns_a_branch() {
    let paths = Arc::new(Mutex::new(Vec::new()));
    let repository_service = Arc::new(FixedRepositoryService {
        paths: Arc::clone(&paths),
        result: Ok(branch_repository()),
        panic_on_open: false,
    });

    let response = invoke_command(
        "repository_open",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": "/tmp/Проекти/change_lens",
        })),
        picker_returning(Ok(None)),
        repository_service,
    )
    .expect("a branch repository should be returned");

    assert_eq!(
        paths
            .lock()
            .expect("the recorded repository paths should be available")
            .as_slice(),
        ["/tmp/Проекти/change_lens"]
    );
    assert_eq!(
        response,
        serde_json::json!({
            "repositoryId": "00000000-0000-0000-0000-000000000000",
            "repository": {
                "name": "change_lens",
                "canonicalPath": "/projects/change_lens",
                "head": {
                    "kind": "branch",
                    "name": "main",
                    "revision": SHA1_REVISION,
                },
            },
            "preferredTarget": null,
        })
    );
}

#[test]
fn repository_open_returns_a_detached_head_shape() {
    let response = invoke_command(
        "repository_open",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": "/projects/change_lens",
        })),
        picker_returning(Ok(None)),
        repository_returning(Ok(detached_repository())),
    )
    .expect("a detached repository should be returned");

    assert_eq!(
        response,
        serde_json::json!({
            "repositoryId": "00000000-0000-0000-0000-000000000000",
            "repository": {
                "name": "change_lens",
                "canonicalPath": "/projects/change_lens",
                "head": {
                    "kind": "detached",
                    "revision": SHA1_REVISION,
                },
            },
            "preferredTarget": null,
        })
    );
}

#[test]
fn repository_history_commands_expose_typed_arguments_and_results() {
    let removed_ids = Arc::new(Mutex::new(Vec::new()));
    let service = Arc::new(LocalStateRepositoryService {
        removed_ids: Arc::clone(&removed_ids),
    });

    let restore = invoke_command(
        "repository_restore_last",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Ok(None)),
        service.clone(),
    )
    .expect("repository restoration should return a tagged result");
    assert_eq!(restore, serde_json::json!({"state": "none"}));

    let history = invoke_command(
        "repository_list_recent",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Ok(None)),
        service.clone(),
    )
    .expect("recent repositories should return typed history");
    assert_eq!(
        history,
        serde_json::json!({
            "lastRepositoryId": "01234567-89ab-cdef-0123-456789abcdef",
            "repositories": [{
                "repositoryId": "01234567-89ab-cdef-0123-456789abcdef",
                "name": "change_lens",
                "canonicalPath": REPOSITORY_PATH,
                "lastOpenedAtUnixMilliseconds": 1_785_081_600_000_u64,
                "preferredTarget": COMPARISON_TARGET,
            }],
        })
    );

    let removal = invoke_command(
        "repository_remove_recent",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "repositoryId": "01234567-89ab-cdef-0123-456789abcdef",
        })),
        picker_returning(Ok(None)),
        service,
    )
    .expect("repository removal should return a payload-free result");
    assert_eq!(removal, serde_json::Value::Null);
    assert_eq!(
        removed_ids
            .lock()
            .expect("the removed repository identifiers should be available")
            .as_slice(),
        ["01234567-89ab-cdef-0123-456789abcdef"]
    );
}

#[test]
fn preference_commands_expose_typed_arguments_and_results() {
    let set_themes = Arc::new(Mutex::new(Vec::new()));
    let service = Arc::new(FixedColorThemePreferenceService {
        set_themes: Arc::clone(&set_themes),
    });

    let get_result = invoke_preference_command(
        "preference_get_color_theme",
        tauri::ipc::InvokeBody::default(),
        service.clone(),
    )
    .expect("the color-theme read should return a typed result");
    assert_eq!(get_result, serde_json::json!({"colorTheme": "dark"}));

    let set_result = invoke_preference_command(
        "preference_set_color_theme",
        tauri::ipc::InvokeBody::Json(serde_json::json!({"colorTheme": "light"})),
        service,
    )
    .expect("the color-theme write should return a payload-free result");
    assert_eq!(set_result, serde_json::Value::Null);
    assert_eq!(
        set_themes
            .lock()
            .expect("the recorded color themes should be available")
            .as_slice(),
        [ColorTheme::Light]
    );
}

#[test]
fn repository_open_preserves_ordered_engine_errors() {
    let response = invoke_command(
        "repository_open",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": "/projects/change_lens",
        })),
        picker_returning(Ok(None)),
        repository_returning(Err(ordered_operation_error())),
    )
    .expect_err("Engine operation errors should reject the command");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "operation",
            "requestId": "desktop-41",
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
fn repository_task_join_failure_returns_one_sanitized_unexpected_error() {
    let response = invoke_command(
        "repository_open",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": "/projects/change_lens",
        })),
        picker_returning(Ok(None)),
        Arc::new(FixedRepositoryService {
            paths: Arc::new(Mutex::new(Vec::new())),
            result: Ok(branch_repository()),
            panic_on_open: true,
        }),
    )
    .expect_err("a repository task panic should reject the command");

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
fn rust_errors_are_reported_once_and_engine_operation_errors_are_not_relogged() {
    let output = Command::new(
        std::env::current_exe()
            .expect("the repository command test executable should be available"),
    )
    .args([
        "--exact",
        "diagnostic_child_process",
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

    assert_eq!(
        standard_error
            .matches("\"event\":\"engine.actionFailed\"")
            .count(),
        1
    );
    assert_eq!(
        standard_error
            .matches("desktop.folderPickerUnavailable")
            .count(),
        1
    );
    assert!(!standard_error.contains("fixture.first"));
    assert!(!standard_error.contains("fixture.second"));
}

#[test]
fn engine_backed_states_share_one_process_and_exit_request_runs_graceful_shutdown_handler() {
    let marker_path = unique_fixture_path("shared-comparison-state");
    let engine_client = Arc::new(shared_engine_client(&marker_path));
    let app = configure_desktop(
        mock_builder(),
        EngineStatusState::new(engine_client.clone()),
        RepositoryState::new(engine_client.clone()),
        RepositoryFolderPickerState::new(Arc::new(FixedRepositoryFolderPicker {
            result: Ok(None),
        })),
        ComparisonState::new(engine_client.clone()),
        AnalysisState::new(engine_client.clone()),
    )
    .build(mock_context(noop_assets()))
    .expect("the test desktop application should build");

    app.state::<RepositoryState>()
        .service()
        .open_repository(REPOSITORY_PATH)
        .expect("the shared engine should open the repository");
    let process_id = engine_client
        .process_id_for_testing()
        .expect("the repository action must start the shared Engine process");

    app.state::<ComparisonState>()
        .service()
        .check_freshness(
            REPOSITORY_PATH,
            COMPARISON_TARGET,
            COMPARISON_FRESHNESS_TOKEN,
        )
        .expect("the comparison action should reuse the shared Engine process");

    assert_eq!(engine_client.process_id_for_testing(), Some(process_id));

    let webview = tauri::WebviewWindowBuilder::new(&app, "main", Default::default())
        .build()
        .expect("the test webview should build");
    let shutdown_client = Arc::clone(&engine_client);
    let close_window = std::thread::spawn(move || {
        webview
            .close()
            .expect("closing the final test window should request application exit");
    });

    app.run_return(move |_app_handle, event| handle_desktop_run_event(&shutdown_client, &event));
    close_window
        .join()
        .expect("the test window close task should complete");

    assert_eq!(engine_client.process_id_for_testing(), None);
    assert_eq!(
        fs::read_to_string(&marker_path).expect("the shared engine must observe graceful EOF"),
        "eof"
    );
    fs::remove_file(&marker_path).expect("the fixture EOF marker should be removed");
}

#[test]
fn diagnostic_child_process() {
    if std::env::var_os(DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE).is_none() {
        return;
    }

    invoke_command(
        "select_repository_folder",
        tauri::ipc::InvokeBody::default(),
        picker_returning(Err(folder_picker_unavailable())),
        repository_returning(Ok(branch_repository())),
    )
    .expect_err("the picker failure should reach the diagnostic boundary");

    invoke_command(
        "repository_open",
        tauri::ipc::InvokeBody::Json(serde_json::json!({
            "path": "/projects/change_lens",
        })),
        picker_returning(Ok(None)),
        repository_returning(Err(ordered_operation_error())),
    )
    .expect_err("the operation error should reach the command boundary");
}

fn invoke_command(
    command: &str,
    body: tauri::ipc::InvokeBody,
    picker: Arc<dyn RepositoryFolderPicker>,
    repository_service: Arc<dyn RepositoryService>,
) -> Result<serde_json::Value, serde_json::Value> {
    let app = configure_desktop(
        mock_builder(),
        EngineStatusState::new(Arc::new(SuccessfulEngineStatusService)),
        RepositoryState::new(repository_service),
        RepositoryFolderPickerState::new(picker),
        ComparisonState::new(Arc::new(UnusedComparisonService)),
        AnalysisState::new(Arc::new(EngineClient::new())),
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

fn invoke_preference_command(
    command: &str,
    body: tauri::ipc::InvokeBody,
    preference_service: Arc<dyn ColorThemePreferenceService>,
) -> Result<serde_json::Value, serde_json::Value> {
    let app = configure_desktop_with_preferences(
        mock_builder(),
        EngineStatusState::new(Arc::new(SuccessfulEngineStatusService)),
        RepositoryState::new(repository_returning(Ok(branch_repository()))),
        RepositoryFolderPickerState::new(picker_returning(Ok(None))),
        ComparisonState::new(Arc::new(UnusedComparisonService)),
        AnalysisState::new(Arc::new(EngineClient::new())),
        PreferenceState::new(preference_service),
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

fn picker_returning(
    result: Result<Option<PathBuf>, EngineActionError>,
) -> Arc<dyn RepositoryFolderPicker> {
    Arc::new(FixedRepositoryFolderPicker { result })
}

fn repository_returning(
    result: Result<RepositoryDescriptor, EngineActionError>,
) -> Arc<dyn RepositoryService> {
    Arc::new(FixedRepositoryService {
        paths: Arc::new(Mutex::new(Vec::new())),
        result,
        panic_on_open: false,
    })
}

fn folder_picker_unavailable() -> EngineActionError {
    EngineActionError {
        kind: ActionErrorKind::Transport,
        request_id: None,
        errors: vec![ActionErrorDetail {
            error_type: OperationErrorType::ExternalDependencyFailure,
            code: "desktop.folderPickerUnavailable".into(),
            message: "The desktop folder picker is unavailable.".into(),
        }],
    }
}

fn ordered_operation_error() -> EngineActionError {
    EngineActionError {
        kind: ActionErrorKind::Operation,
        request_id: Some("desktop-41".into()),
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

fn branch_repository() -> RepositoryDescriptor {
    RepositoryDescriptor {
        name: "change_lens".into(),
        canonical_path: "/projects/change_lens".into(),
        head: RepositoryHead::Branch {
            name: "main".into(),
            revision: SHA1_REVISION.into(),
        },
    }
}

fn detached_repository() -> RepositoryDescriptor {
    RepositoryDescriptor {
        name: "change_lens".into(),
        canonical_path: "/projects/change_lens".into(),
        head: RepositoryHead::Detached {
            revision: SHA1_REVISION.into(),
        },
    }
}

fn shared_engine_client(marker_path: &Path) -> EngineClient {
    static FIXTURE_BUILD: OnceLock<()> = OnceLock::new();
    FIXTURE_BUILD.get_or_init(|| build_dotnet_project(&fixture_project_path()));

    EngineClient::with_engine_path_and_arguments(
        fixture_dll_path(),
        vec![
            "record-eof".into(),
            marker_path.to_string_lossy().into_owned(),
        ],
    )
}

fn fixture_dll_path() -> PathBuf {
    fixture_project_path()
        .parent()
        .expect("the fixture project must have a parent directory")
        .join("bin/Debug/net10.0/ChangeLens.EngineProtocolFixture.dll")
}

fn fixture_project_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join(
        "../../../tests/integration/desktop/EngineStatus/Fixtures/ChangeLens.EngineProtocolFixture/ChangeLens.EngineProtocolFixture.csproj",
    )
}

fn build_dotnet_project(project: &Path) {
    let status = Command::new("dotnet")
        .arg("build")
        .arg(project)
        .arg("--nologo")
        .status()
        .expect("the dotnet CLI should build the fixture project");

    assert!(status.success(), "the fixture project build should pass");
}

fn unique_fixture_path(name: &str) -> PathBuf {
    let timestamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("the system clock should be after the Unix epoch")
        .as_nanos();

    std::env::temp_dir().join(format!(
        "changelens-{name}-{}-{timestamp}.txt",
        std::process::id()
    ))
}
