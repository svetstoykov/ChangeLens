use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct ComparisonListTargetsParameters<'a> {
    pub(crate) path: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) query: Option<&'a str>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) after: Option<&'a str>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) target_set_token: Option<&'a str>,
}
