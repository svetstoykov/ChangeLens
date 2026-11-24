use changelens_desktop_lib::analysis::AnalysisState;
use changelens_desktop_lib::comparisons::{
    ComparisonFreshness, ComparisonRefreshRemoteBaselineResult, ComparisonRemoteBaseline,
    ComparisonService, ComparisonState, ComparisonTargetPage, PreparedComparison,
};
use changelens_desktop_lib::configure_desktop_with_preferences;
use changelens_desktop_lib::engine_protocol::{EngineActionError, EngineClient};
use changelens_desktop_lib::engine_status::{EngineStatusService, EngineStatusState};
use changelens_desktop_lib::preferences::{
    ColorTheme, ColorThemePreference, ColorThemePreferenceService, PreferenceState,
};
use changelens_desktop_lib::repositories::{
    RepositoryDescriptor, RepositoryFolderPicker, RepositoryFolderPickerState, RepositoryService,
    RepositoryState,
};
use raw_window_handle::HasWindowHandle;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use tauri::test::{INVOKE_KEY, get_ipc_response, mock_builder, mock_context, noop_assets};

struct UnusedEngineStatusService;

impl EngineStatusService for UnusedEngineStatusService {
    fn check_status(&self) -> Result<(), EngineActionError> {
        unreachable!("the preference command test does not check engine status")
    }
}

struct UnusedRepositoryFolderPicker;

impl RepositoryFolderPicker for UnusedRepositoryFolderPicker {
    fn select_folder(
        &self,
        _owner: &dyn HasWindowHandle,
    ) -> Result<Option<PathBuf>, EngineActionError> {
        unreachable!("the preference command test does not open a folder picker")
    }
}

struct UnusedRepositoryService;

impl RepositoryService for UnusedRepositoryService {
    fn open_repository(&self, _path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        unreachable!("the preference command test does not open repositories")
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
        unreachable!("the preference command test does not list comparison targets")
    }

    fn prepare(&self, _path: &str, _target: &str) -> Result<PreparedComparison, EngineActionError> {
        unreachable!("the preference command test does not prepare comparisons")
    }

    fn check_freshness(
        &self,
        _path: &str,
        _target: &str,
        _freshness_token: &str,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        unreachable!("the preference command test does not check comparison freshness")
    }

    fn check_remote_baseline(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<ComparisonRemoteBaseline, EngineActionError> {
        unreachable!("the preference command test does not check the remote baseline")
    }

    fn refresh_remote_baseline(
        &self,
        _path: &str,
        _target: &str,
    ) -> Result<ComparisonRefreshRemoteBaselineResult, EngineActionError> {
        unreachable!("the preference command test does not refresh the remote baseline")
    }
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

fn invoke_preference_command(
    command: &str,
    body: tauri::ipc::InvokeBody,
    preference_service: Arc<dyn ColorThemePreferenceService>,
) -> Result<serde_json::Value, serde_json::Value> {
    let app = configure_desktop_with_preferences(
        mock_builder(),
        EngineStatusState::new(Arc::new(UnusedEngineStatusService)),
        RepositoryState::new(Arc::new(UnusedRepositoryService)),
        RepositoryFolderPickerState::new(Arc::new(UnusedRepositoryFolderPicker)),
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
