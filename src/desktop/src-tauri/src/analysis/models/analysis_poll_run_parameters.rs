use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct AnalysisPollRunParameters<'a> {
    pub(crate) run_id: &'a str,
}
