use crate::engine_protocol::ActionErrorDetail;
use serde::Deserialize;

#[derive(Deserialize)]
#[serde(tag = "type", deny_unknown_fields)]
pub(crate) enum EngineResponse<T> {
    #[serde(rename = "result")]
    Result {
        #[serde(rename = "protocolVersion")]
        protocol_version: u32,
        #[serde(rename = "requestId")]
        request_id: String,
        result: T,
    },
    #[serde(rename = "error")]
    Error {
        #[serde(rename = "protocolVersion")]
        protocol_version: u32,
        #[serde(rename = "requestId")]
        request_id: Option<String>,
        errors: Vec<ActionErrorDetail>,
    },
}
