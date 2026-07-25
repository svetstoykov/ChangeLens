use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct ComparisonCheckFreshnessParameters<'a> {
    pub(crate) path: &'a str,
    pub(crate) target: &'a str,
    pub(crate) freshness_token: &'a str,
}
