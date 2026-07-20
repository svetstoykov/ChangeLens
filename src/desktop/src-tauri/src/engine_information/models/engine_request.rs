use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct EngineRequest<'a> {
    pub(crate) protocol_version: u32,
    pub(crate) request_id: &'a str,
    pub(crate) method: &'a str,
}
