use crate::analysis::models::validation::deserialize_run_id;
use serde::{Deserialize, Serialize};

/// Represents the tagged analysis-start outcome union in the engine protocol.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(
    tag = "state",
    rename_all = "camelCase",
    rename_all_fields = "camelCase",
    deny_unknown_fields
)]
pub enum AnalysisStartResult {
    Accepted {
        #[serde(deserialize_with = "deserialize_run_id")]
        run_id: String,
        requested_at: u64,
    },
    RejectedStale,
    RejectedActive {
        #[serde(deserialize_with = "deserialize_run_id")]
        active_run_id: String,
    },
}
