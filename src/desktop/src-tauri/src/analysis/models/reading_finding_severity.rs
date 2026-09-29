use serde::{Deserialize, Serialize};

/// Ranks how serious a published finding is.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingFindingSeverity {
    Critical,
    Warning,
    Info,
}
