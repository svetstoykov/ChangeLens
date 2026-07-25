use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct ComparisonPrepareParameters<'a> {
    pub(crate) path: &'a str,
    pub(crate) target: &'a str,
}
