use crate::comparisons::ComparisonTarget;
use crate::comparisons::models::validation::{
    deserialize_count, deserialize_token, validate_ref_name,
};
use serde::de::Error;
use serde::{Deserialize, Serialize};

/// Represents one bounded page of available comparison targets.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ComparisonTargetPage {
    /// The targets in deterministic display order.
    pub targets: Vec<ComparisonTarget>,
    /// The conservative default target, when available.
    pub suggested_target: Option<ComparisonTarget>,
    /// The cursor for a later page, when more supported targets remain.
    pub next_cursor: Option<String>,
    /// The immutable target-set identity paired with cursors.
    #[serde(deserialize_with = "deserialize_token")]
    pub target_set_token: String,
    /// The count of discovered targets excluded as unsupported.
    #[serde(deserialize_with = "deserialize_count")]
    pub unsupported_target_count: u32,
}

impl<'de> Deserialize<'de> for ComparisonTargetPage {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let value = serde_json::Value::deserialize(deserializer)?;
        let object = value
            .as_object()
            .ok_or_else(|| D::Error::custom("the target page must be an object"))?;
        let required = [
            "targets",
            "suggestedTarget",
            "nextCursor",
            "targetSetToken",
            "unsupportedTargetCount",
        ];
        if object.len() != required.len() || required.iter().any(|key| !object.contains_key(*key)) {
            return Err(D::Error::custom(
                "the target page must contain exactly the protocol result properties",
            ));
        }

        let targets = value_from(object, "targets")?;
        let suggested_target = value_from(object, "suggestedTarget")?;
        let next_cursor: Option<String> = value_from(object, "nextCursor")?;
        if let Some(cursor) = next_cursor.as_deref() {
            validate_ref_name(cursor).map_err(D::Error::custom)?;
        }
        let Token(target_set_token) = value_from(object, "targetSetToken")?;
        let Count(unsupported_target_count) = value_from(object, "unsupportedTargetCount")?;

        Ok(Self {
            targets,
            suggested_target,
            next_cursor,
            target_set_token,
            unsupported_target_count,
        })
    }
}

#[derive(Deserialize)]
struct Token(#[serde(deserialize_with = "deserialize_token")] String);

#[derive(Deserialize)]
struct Count(#[serde(deserialize_with = "deserialize_count")] u32);

fn value_from<T, E>(object: &serde_json::Map<String, serde_json::Value>, name: &str) -> Result<T, E>
where
    T: serde::de::DeserializeOwned,
    E: serde::de::Error,
{
    let value = object
        .get(name)
        .expect("the caller checked every required target-page property");
    serde_json::from_value(value.clone()).map_err(E::custom)
}
