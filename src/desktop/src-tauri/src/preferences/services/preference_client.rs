use crate::engine_protocol::{EngineActionError, EngineClient};
use crate::preferences::constants::{
    GET_COLOR_THEME_ACTION, PREFERENCE_RESPONSE_TIMEOUT, SET_COLOR_THEME_ACTION,
};
use crate::preferences::models::SetColorThemeParameters;
use crate::preferences::{ColorTheme, ColorThemePreference, ColorThemePreferenceService};
use serde::Deserialize;

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
