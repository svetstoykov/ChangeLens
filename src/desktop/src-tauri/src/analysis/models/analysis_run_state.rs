use serde::{Deserialize, Serialize};

/// Defines the durable lifecycle state of an analysis run.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum AnalysisRunState {
    PendingCapture,
    Capturing,
    Discovering,
    Collecting,
    Persisting,
    Completed,
    CompletedWithLimitations,
    Cancelled,
    Failed,
    Interrupted,
}
