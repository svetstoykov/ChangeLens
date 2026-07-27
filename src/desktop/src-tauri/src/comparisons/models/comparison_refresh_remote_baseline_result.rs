use serde::{Deserialize, Serialize};

/// Represents the new revision of a refreshed remote-tracking reference.
#[derive(Clone, Debug, PartialEq, Eq, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ComparisonRefreshRemoteBaselineResult {
    /// The new full object identifier of the refreshed remote-tracking reference.
    pub remote_revision: String,
}
