use serde::{Deserialize, Serialize};

/// Identifies the pipeline stage that omitted unresolved reading evidence.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingOmissionSourceKind {
    FileNotRead,
    NoEvidenceSelected,
    PolicyExcluded,
}
