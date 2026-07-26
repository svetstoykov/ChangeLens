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

/// Represents the optional explicit color-theme preference.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ColorThemePreference {
    /// The explicit theme, or `None` to follow the operating system.
    pub color_theme: Option<ColorTheme>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct SetColorThemeParameters {
    pub(crate) color_theme: ColorTheme,
}

#[cfg(test)]
mod tests {
    use super::{ColorTheme, ColorThemePreference};

    const FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/preferences-get-color-theme.result.json"
    ));

    #[test]
    fn parses_the_shared_color_theme_fixture_strictly() {
        let envelope: serde_json::Value = serde_json::from_str(FIXTURE).unwrap();
        let preference: ColorThemePreference =
            serde_json::from_value(envelope["result"].clone()).unwrap();
        assert_eq!(preference.color_theme, Some(ColorTheme::Dark));

        serde_json::from_str::<ColorThemePreference>(r#"{"colorTheme":"system"}"#)
            .expect_err("unknown themes must be rejected");
    }
}
