use crate::comparisons::models::validation::validate_count;
use serde::de::Error;
use serde::{Deserialize, Serialize};

/// Defines the current readiness of a prepared comparison.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(
    tag = "state",
    rename_all = "camelCase",
    rename_all_fields = "camelCase"
)]
pub enum ComparisonReadiness {
    /// The comparison has changes that can be analyzed.
    Ready,
    /// The comparison has no changes.
    Empty,
    /// The working tree has unresolved conflicts.
    Conflicts {
        /// The number of conflicted file lineages.
        conflicted_file_count: u32,
    },
}

impl<'de> Deserialize<'de> for ComparisonReadiness {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let value = serde_json::Value::deserialize(deserializer)?;
        let object = value
            .as_object()
            .ok_or_else(|| D::Error::custom("the readiness must be an object"))?;
        let state = object
            .get("state")
            .and_then(serde_json::Value::as_str)
            .ok_or_else(|| D::Error::custom("the readiness state is required"))?;

        match state {
            "ready" if object.len() == 1 => Ok(Self::Ready),
            "empty" if object.len() == 1 => Ok(Self::Empty),
            "conflicts" if object.len() == 2 => {
                let count = object
                    .get("conflictedFileCount")
                    .ok_or_else(|| D::Error::custom("the conflicted file count is required"))?;
                let count = serde_json::from_value::<u64>(count.clone())
                    .map_err(D::Error::custom)
                    .and_then(|value| validate_count(value).map_err(D::Error::custom))?;
                Ok(Self::Conflicts {
                    conflicted_file_count: count,
                })
            }
            "ready" | "empty" | "conflicts" => Err(D::Error::custom(
                "the readiness properties do not match its state",
            )),
            _ => Err(D::Error::custom("the readiness state is not supported")),
        }
    }
}
