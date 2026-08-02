use crate::analysis::models::validation::{deserialize_optional_guid, deserialize_run_id};
use crate::analysis::models::{
    AnalysisComparison, AnalysisFact, AnalysisRepository, AnalysisRunState, AnalysisTerminal,
};
use serde::de::Error;
use serde::{Deserialize, Serialize};

/// Represents the current-state analysis run summary shared by poll and active-lookup results.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AnalysisRunSummary {
    #[serde(deserialize_with = "deserialize_run_id")]
    pub run_id: String,
    pub state: AnalysisRunState,
    pub repository: AnalysisRepository,
    pub comparison: AnalysisComparison,
    pub requested_at: u64,
    pub capture_started_at: Option<u64>,
    pub captured_at: Option<u64>,
    #[serde(deserialize_with = "deserialize_optional_guid")]
    pub snapshot_id: Option<String>,
    pub cancellation_requested: bool,
    #[serde(deserialize_with = "deserialize_bounded_facts")]
    pub facts: Vec<AnalysisFact>,
    pub terminal: Option<AnalysisTerminal>,
    pub interrupted_at: Option<u64>,
    #[serde(deserialize_with = "deserialize_interruption_reason")]
    pub interruption_reason: Option<String>,
}

fn deserialize_bounded_facts<'de, D>(deserializer: D) -> Result<Vec<AnalysisFact>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let facts = Vec::<AnalysisFact>::deserialize(deserializer)?;

    if facts.len() > 32 {
        return Err(D::Error::custom(
            "the facts collection must contain at most 32 entries",
        ));
    }

    Ok(facts)
}

fn deserialize_interruption_reason<'de, D>(deserializer: D) -> Result<Option<String>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = Option::<String>::deserialize(deserializer)?;

    if value
        .as_deref()
        .is_some_and(|reason| reason != "engineStopped")
    {
        return Err(D::Error::custom(
            "the interruption reason is not a controlled value",
        ));
    }

    Ok(value)
}
