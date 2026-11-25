use super::ColorTheme;
use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct SetColorThemeParameters {
    pub(crate) color_theme: ColorTheme,
}
