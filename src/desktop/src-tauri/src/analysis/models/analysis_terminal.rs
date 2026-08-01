use crate::analysis::models::validation::deserialize_failure_code;
use serde::{Deserialize, Serialize};

/// Represents the strict terminal outcome union in the engine protocol.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(
    tag = "kind",
    rename_all = "camelCase",
    rename_all_fields = "camelCase",
    deny_unknown_fields
)]
pub enum AnalysisTerminal {
    Completed {
        terminal_at: u64,
    },
    CompletedWithLimitations {
        terminal_at: u64,
        limitation_count: u32,
    },
    Cancelled {
        terminal_at: u64,
    },
    Failed {
        terminal_at: u64,
        #[serde(deserialize_with = "deserialize_failure_code")]
        failure_code: String,
    },
}
