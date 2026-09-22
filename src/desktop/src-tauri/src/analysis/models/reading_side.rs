use serde::{Deserialize, Serialize};

/// Identifies the comparison side a citation or evidence node belongs to.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingSide {
    Before,
    After,
}
