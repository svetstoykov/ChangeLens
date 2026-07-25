use crate::comparisons::ComparisonTargetKind;
use crate::comparisons::models::validation::{
    deserialize_non_blank, deserialize_ref_name, deserialize_revision,
};
use serde::{Deserialize, Serialize};

/// Represents one exact local or cached remote-tracking comparison target.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ComparisonTarget {
    /// The target source.
    pub kind: ComparisonTargetKind,
    /// The display name for the target.
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub name: String,
    /// The exact full reference used for comparison actions.
    #[serde(deserialize_with = "deserialize_ref_name")]
    pub full_name: String,
    /// The full object revision currently resolved for the target.
    #[serde(deserialize_with = "deserialize_revision")]
    pub revision: String,
}
