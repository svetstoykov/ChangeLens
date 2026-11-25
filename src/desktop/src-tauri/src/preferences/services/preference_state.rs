use crate::engine_protocol::EngineActionError;
use crate::preferences::{ColorTheme, ColorThemePreference, ColorThemePreferenceService};
use std::sync::Arc;

/// Stores the preference service shared by Tauri commands.
pub struct PreferenceState(Arc<dyn ColorThemePreferenceService>);

impl PreferenceState {
    /// Creates preference command state backed by `service`.
    pub fn new(service: Arc<dyn ColorThemePreferenceService>) -> Self {
        Self(service)
    }

    /// Returns the configured preference service.
    pub fn service(&self) -> Arc<dyn ColorThemePreferenceService> {
        Arc::clone(&self.0)
    }

    #[doc(hidden)]
    pub fn unused() -> Self {
        Self(Arc::new(UnusedColorThemePreferenceService))
    }
}

struct UnusedColorThemePreferenceService;

impl ColorThemePreferenceService for UnusedColorThemePreferenceService {
    fn get_color_theme(&self) -> Result<ColorThemePreference, EngineActionError> {
        unreachable!("preference actions are not configured for this desktop test")
    }

    fn set_color_theme(&self, _color_theme: ColorTheme) -> Result<(), EngineActionError> {
        unreachable!("preference actions are not configured for this desktop test")
    }
}
