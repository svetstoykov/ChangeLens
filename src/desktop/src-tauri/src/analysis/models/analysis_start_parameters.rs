use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct AnalysisStartParameters<'a> {
    pub(crate) path: &'a str,
    pub(crate) target: &'a str,
    pub(crate) freshness_token: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) change_context: Option<&'a str>,
}
