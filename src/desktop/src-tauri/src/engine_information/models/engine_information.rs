use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct EngineInformation {
    pub name: String,
    pub version: String,
    pub protocol_version: u32,
}
