use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ActionErrorKind {
    Operation,
    Transport,
    Protocol,
    Unexpected,
}
