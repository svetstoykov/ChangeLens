use serde::{Deserialize, Serialize};

/// Describes the verification trust level of a claim or citation provenance.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingTrust {
    Unchecked,
    Derived,
    Checked,
}
