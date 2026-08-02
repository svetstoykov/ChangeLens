use crate::analysis::models::validation::{
    deserialize_non_blank, deserialize_repository_id, deserialize_revision,
};
use serde::{Deserialize, Serialize};

/// Represents the immutable accepted repository identity in an analysis summary.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AnalysisRepository {
    #[serde(deserialize_with = "deserialize_repository_id")]
    pub repository_id: String,
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub display_name: String,
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub canonical_path: String,
    #[serde(deserialize_with = "deserialize_revision")]
    pub head: String,
}
