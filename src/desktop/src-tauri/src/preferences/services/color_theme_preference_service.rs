use crate::engine_protocol::EngineActionError;
use crate::preferences::{ColorTheme, ColorThemePreference};

/// Defines approved color-theme preference actions.
pub trait ColorThemePreferenceService: Send + Sync {
    /// Gets the optional explicit theme.
    fn get_color_theme(&self) -> Result<ColorThemePreference, EngineActionError>;
    /// Stores an explicit theme.
    fn set_color_theme(&self, color_theme: ColorTheme) -> Result<(), EngineActionError>;
}
