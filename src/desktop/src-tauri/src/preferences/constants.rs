use std::time::Duration;

pub(crate) const GET_COLOR_THEME_ACTION: &str = "preferences.getColorTheme";
pub(crate) const SET_COLOR_THEME_ACTION: &str = "preferences.setColorTheme";
pub(crate) const PREFERENCE_RESPONSE_TIMEOUT: Duration = Duration::from_secs(5);
