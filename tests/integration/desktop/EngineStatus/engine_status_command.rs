use changelens_desktop_lib::configure_desktop;
use changelens_desktop_lib::engine_protocol::{
    ActionErrorDetail, ActionErrorKind, EngineActionError, OperationErrorType,
};
use changelens_desktop_lib::engine_status::{EngineStatusService, EngineStatusState};
use changelens_desktop_lib::repositories::{
    RepositoryDescriptor, RepositoryFolderPicker, RepositoryFolderPickerState, RepositoryService,
    RepositoryState,
};
use std::path::PathBuf;
use std::sync::Arc;
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

struct FixedEngineStatusService {
    result: Result<(), EngineActionError>,
}

impl EngineStatusService for FixedEngineStatusService {
    fn check_status(&self) -> Result<(), EngineActionError> {
        self.result.clone()
    }
}

struct UnusedRepositoryFolderPicker;

impl RepositoryFolderPicker for UnusedRepositoryFolderPicker {
    fn select_folder(&self) -> Result<Option<PathBuf>, EngineActionError> {
        unreachable!("the engine status test does not open a folder picker")
    }
}

struct UnusedRepositoryService;

impl RepositoryService for UnusedRepositoryService {
    fn open_repository(&self, _path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        unreachable!("the engine status test does not open a repository")
    }
}

#[test]
fn invokes_registered_engine_status_command_through_managed_state() {
    let response = invoke_engine_status(Ok(()))
        .expect("the registered command should return a successful IPC response");

    assert_eq!(response, serde_json::Value::Null);
}

#[test]
fn serializes_registered_engine_status_command_error() {
    let response = invoke_engine_status(Err(EngineActionError {
        kind: ActionErrorKind::Operation,
        request_id: Some("desktop-43".into()),
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
    }))
    .expect_err("the registered command should return a failed IPC response");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "operation",
            "requestId": "desktop-43",
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
fn serializes_registered_engine_status_command_error_without_request_id() {
    let response = invoke_engine_status(Err(EngineActionError {
        kind: ActionErrorKind::Operation,
        request_id: None,
        errors: vec![ActionErrorDetail {
            error_type: OperationErrorType::Validation,
            code: "protocol.invalidRequest".into(),
            message: "The request does not match the engine protocol schema.".into(),
        }],
    }))
    .expect_err("the registered command should return a failed IPC response");

    assert_eq!(
        response,
        serde_json::json!({
            "kind": "operation",
            "errors": [
                {
                    "type": "Validation",
                    "code": "protocol.invalidRequest",
                    "message": "The request does not match the engine protocol schema.",
                },
            ],
        })
    );
}

fn invoke_engine_status(
    result: Result<(), EngineActionError>,
) -> Result<serde_json::Value, serde_json::Value> {
    let app = configure_desktop(
        mock_builder(),
        EngineStatusState::new(Arc::new(FixedEngineStatusService { result })),
        RepositoryState::new(Arc::new(UnusedRepositoryService)),
        RepositoryFolderPickerState::new(Arc::new(UnusedRepositoryFolderPicker)),
    )
    .build(mock_context(noop_assets()))
    .expect("the test desktop application should build");
    let webview = tauri::WebviewWindowBuilder::new(&app, "main", Default::default())
        .build()
        .expect("the test webview should build");
    let request = tauri::webview::InvokeRequest {
        cmd: "engine_check_status".into(),
        callback: tauri::ipc::CallbackFn(0),
        error: tauri::ipc::CallbackFn(1),
        url: if cfg!(any(windows, target_os = "android")) {
            "http://tauri.localhost"
        } else {
            "tauri://localhost"
        }
        .parse()
        .expect("the test IPC URL should be valid"),
        body: tauri::ipc::InvokeBody::default(),
        headers: Default::default(),
        invoke_key: INVOKE_KEY.to_string(),
    };

    get_ipc_response(&webview, request)
        .map(|body| body.deserialize().expect("the success body should be JSON"))
}
