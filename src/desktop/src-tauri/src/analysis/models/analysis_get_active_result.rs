use crate::analysis::models::AnalysisRunProjection;
use serde::{Deserialize, Serialize};

/// Represents the tagged active-run lookup union in the engine protocol.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(
    tag = "state",
    rename_all = "camelCase",
    rename_all_fields = "camelCase",
    deny_unknown_fields
)]
pub enum AnalysisGetActiveResult {
    None,
    Active { run: Box<AnalysisRunProjection> },
}
