use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct RepositoryRemoveRecentParameters<'a> {
    pub(crate) repository_id: &'a str,
}
