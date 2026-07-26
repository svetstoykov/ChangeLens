use crate::engine_protocol::{EngineActionError, EngineClient};
use crate::preferences::constants::{
    GET_COLOR_THEME_ACTION, PREFERENCE_RESPONSE_TIMEOUT, SET_COLOR_THEME_ACTION,
};
use crate::preferences::models::SetColorThemeParameters;
use crate::preferences::{ColorTheme, ColorThemePreference};
use serde::Deserialize;
use std::sync::Arc;

/// Defines approved color-theme preference actions.
pub trait ColorThemePreferenceService: Send + Sync {
    /// Gets the optional explicit theme.
    fn get_color_theme(&self) -> Result<ColorThemePreference, EngineActionError>;
    /// Stores an explicit theme.
    fn set_color_theme(&self, color_theme: ColorTheme) -> Result<(), EngineActionError>;
}

impl ColorThemePreferenceService for EngineClient {
    fn get_color_theme(&self) -> Result<ColorThemePreference, EngineActionError> {
        self.execute_action::<Option<()>, ColorThemePreference>(
            GET_COLOR_THEME_ACTION,
            None,
            PREFERENCE_RESPONSE_TIMEOUT,
        )
    }

    fn set_color_theme(&self, color_theme: ColorTheme) -> Result<(), EngineActionError> {
        self.execute_action::<_, NullResult>(
            SET_COLOR_THEME_ACTION,
            Some(SetColorThemeParameters { color_theme }),
            PREFERENCE_RESPONSE_TIMEOUT,
        )
        .map(|_| ())
    }
}

#[derive(Deserialize)]
struct NullResult;

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
