use crate::analysis::models::validation::{
    deserialize_ref_name, deserialize_revision, deserialize_token,
};
use serde::{Deserialize, Serialize};

/// Represents the immutable accepted comparison identity in an analysis projection.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AnalysisComparison {
    #[serde(deserialize_with = "deserialize_ref_name")]
    pub target: String,
    #[serde(deserialize_with = "deserialize_revision")]
    pub target_revision: String,
    #[serde(deserialize_with = "deserialize_token")]
    pub freshness_token: String,
}
