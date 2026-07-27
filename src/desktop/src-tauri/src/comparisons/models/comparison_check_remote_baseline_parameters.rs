use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct ComparisonCheckRemoteBaselineParameters<'a> {
    pub(crate) path: &'a str,
    pub(crate) target: &'a str,
}
