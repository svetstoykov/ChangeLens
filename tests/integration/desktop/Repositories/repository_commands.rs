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
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

const SHA1_REVISION: &str = "0123456789abcdef0123456789abcdef01234567";
const DIAGNOSTIC_CHILD_ENVIRONMENT_VARIABLE: &str =
    "CHANGELENS_REPOSITORY_COMMAND_DIAGNOSTIC_CHILD";

struct SuccessfulEngineStatusService;

impl EngineStatusService for SuccessfulEngineStatusService {
    fn check_status(&self) -> Result<(), EngineActionError> {
        Ok(())
    }
}

struct FixedRepositoryFolderPicker {
    result: Result<Option<PathBuf>, EngineActionError>,
}

impl RepositoryFolderPicker for FixedRepositoryFolderPicker {
    fn select_folder(&self) -> Result<Option<PathBuf>, EngineActionError> {
        self.result.clone()
    }
}

struct FixedRepositoryService {
    paths: Arc<Mutex<Vec<String>>>,
    result: Result<RepositoryDescriptor, EngineActionError>,
    panic_on_open: bool,
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
            "name": "change_lens",
            "canonicalPath": "/projects/change_lens",
            "head": {
                "kind": "branch",
                "name": "main",
                "revision": SHA1_REVISION,
            },
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
            "name": "change_lens",
            "canonicalPath": "/projects/change_lens",
            "head": {
                "kind": "detached",
                "revision": SHA1_REVISION,
            },
        })
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
