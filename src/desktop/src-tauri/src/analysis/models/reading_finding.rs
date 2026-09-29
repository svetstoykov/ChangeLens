use crate::analysis::models::ReadingFindingSeverity;
use serde::{Deserialize, Serialize};

/// Represents one published defect finding. Its citations use the claim id `finding:{id}`.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingFinding {
    pub id: String,
    pub severity: ReadingFindingSeverity,
    pub title: String,
    pub trigger: String,
    pub impact: String,
    pub fix: String,
    pub area_id: Option<String>,
}
