use crate::comparisons::models::validation::{
    deserialize_count, deserialize_revision, deserialize_token,
};
use crate::comparisons::{ComparisonReadiness, ComparisonTarget};
use crate::repositories::RepositoryDescriptor;
use serde::{Deserialize, Serialize};

/// Represents the immutable facts prepared for an exact comparison.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct PreparedComparison {
    /// The repository identity and current HEAD used for preparation.
    pub repository: RepositoryDescriptor,
    /// The exact comparison target.
    pub target: ComparisonTarget,
    /// The one selected merge-base revision.
    #[serde(deserialize_with = "deserialize_revision")]
    pub merge_base_revision: String,
    /// The commits reachable only from the current work revision.
    #[serde(deserialize_with = "deserialize_count")]
    pub current_work_commit_count: u32,
    /// The commits reachable only from the target revision.
    #[serde(deserialize_with = "deserialize_count")]
    pub target_only_commit_count: u32,
    /// The total changed file lineages.
    #[serde(deserialize_with = "deserialize_count")]
    pub changed_file_total: u32,
    /// The uncommitted file lineages.
    #[serde(deserialize_with = "deserialize_count")]
    pub uncommitted_file_total: u32,
    /// The file lineages with staged changes.
    #[serde(deserialize_with = "deserialize_count")]
    pub staged_file_count: u32,
    /// The file lineages with unstaged changes.
    #[serde(deserialize_with = "deserialize_count")]
    pub unstaged_file_count: u32,
    /// The file lineages that are untracked.
    #[serde(deserialize_with = "deserialize_count")]
    pub untracked_file_count: u32,
    /// The readiness classification.
    pub readiness: ComparisonReadiness,
    /// The compact fingerprint used for later freshness checks.
    #[serde(deserialize_with = "deserialize_token")]
    pub freshness_token: String,
}
