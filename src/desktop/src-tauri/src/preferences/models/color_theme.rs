use serde::{Deserialize, Serialize};

/// Defines explicit persisted color themes.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum ColorTheme {
    /// The light application theme.
    Light,
    /// The dark application theme.
    Dark,
}
