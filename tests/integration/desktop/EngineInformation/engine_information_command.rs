use changelens_desktop_lib::configure_engine_information;
use changelens_desktop_lib::engine_information::{
    EngineInformation, EngineInformationService, EngineState,
};
use changelens_desktop_lib::engine_protocol::{
    ActionErrorDetail, ActionErrorKind, EngineActionError, OperationErrorType,
};
use std::sync::Arc;
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

struct FixedEngineInformationService {
    result: Result<EngineInformation, EngineActionError>,
}

impl EngineInformationService for FixedEngineInformationService {
    fn get_information(&self) -> Result<EngineInformation, EngineActionError> {
        self.result.clone()
    }
}

#[test]
fn invokes_registered_engine_information_command_through_managed_state() {
    let response = invoke_engine_information(Ok(EngineInformation {
        name: "ChangeLens.Engine".into(),
        version: "0.1.0".into(),
        protocol_version: 1,
    }))
    .expect("the registered command should return a successful IPC response");

    assert_eq!(
        response,
        serde_json::json!({
            "name": "ChangeLens.Engine",
            "version": "0.1.0",
            "protocolVersion": 1,
        })
    );
}

#[test]
fn serializes_registered_engine_information_command_error() {
    let response = invoke_engine_information(Err(EngineActionError {
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

fn invoke_engine_information(
    result: Result<EngineInformation, EngineActionError>,
) -> Result<serde_json::Value, serde_json::Value> {
    let state = EngineState::new(Arc::new(FixedEngineInformationService { result }));
    let app = configure_engine_information(mock_builder(), state)
        .build(mock_context(noop_assets()))
        .expect("the test desktop application should build");
    let webview = tauri::WebviewWindowBuilder::new(&app, "main", Default::default())
        .build()
        .expect("the test webview should build");
    let request = tauri::webview::InvokeRequest {
        cmd: "engine_get_info".into(),
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
