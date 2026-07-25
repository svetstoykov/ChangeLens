use serde::de::Error;
use serde::{Deserialize, Serialize};

/// Defines whether a prepared comparison still matches the repository facts.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(tag = "state", rename_all = "camelCase")]
pub enum ComparisonFreshness {
    /// The prepared comparison remains current.
    Current,
    /// The repository facts have changed since preparation.
    Stale,
}

impl<'de> Deserialize<'de> for ComparisonFreshness {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let value = serde_json::Value::deserialize(deserializer)?;
        let object = value
            .as_object()
            .ok_or_else(|| D::Error::custom("the freshness result must be an object"))?;

        if object.len() != 1 {
            return Err(D::Error::custom(
                "the freshness result must contain only its state",
            ));
        }

        match object.get("state").and_then(serde_json::Value::as_str) {
            Some("current") => Ok(Self::Current),
            Some("stale") => Ok(Self::Stale),
            _ => Err(D::Error::custom("the freshness state is not supported")),
        }
    }
}
