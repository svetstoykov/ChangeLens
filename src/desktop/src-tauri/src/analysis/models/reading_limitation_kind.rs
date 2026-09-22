use serde::{Deserialize, Serialize};

/// Classifies a disclosed reading limitation.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingLimitationKind {
    FileNotRead,
    FileNotQuoted,
    UncommittedWorkExcluded,
}
