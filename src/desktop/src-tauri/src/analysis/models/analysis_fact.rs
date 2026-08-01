use serde::de::Error;
use serde::{Deserialize, Serialize};

/// Represents one bounded discovered-fact summary. Gate 2.1 never produces a populated collection.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AnalysisFact {
    #[serde(deserialize_with = "deserialize_kind")]
    pub kind: String,
    pub count: u32,
    pub detail: Option<String>,
}

fn deserialize_kind<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;

    if value.trim().is_empty() || value.chars().count() > 64 {
        return Err(D::Error::custom(
            "the fact kind must be a nonblank value of at most 64 characters",
        ));
    }

    Ok(value)
}
