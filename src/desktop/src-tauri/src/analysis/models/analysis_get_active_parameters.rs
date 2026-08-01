use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct AnalysisGetActiveParameters<'a> {
    pub(crate) path: &'a str,
}
