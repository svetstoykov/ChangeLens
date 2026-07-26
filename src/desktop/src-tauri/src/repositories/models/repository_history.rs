use crate::repositories::RepositoryDescriptor;
use crate::repositories::models::validation::{deserialize_non_blank, deserialize_repository_id};
use serde::{Deserialize, Serialize};

/// Represents startup repository restoration.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", tag = "state", deny_unknown_fields)]
pub enum RepositoryRestoreResult {
    /// No repository is selected for automatic restoration.
    #[serde(rename = "none")]
    None,
    /// The retained repository was successfully revalidated.
    #[serde(rename = "restored")]
    Restored {
        /// The ChangeLens-generated history identifier.
        #[serde(deserialize_with = "deserialize_repository_id")]
        repository_id: String,
        /// The current repository facts.
        repository: RepositoryDescriptor,
        /// The saved full comparison ref, when one exists.
        preferred_target: Option<String>,
    },
}

/// Represents one retained recent repository.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct RecentRepository {
    /// The ChangeLens-generated history identifier.
    #[serde(deserialize_with = "deserialize_repository_id")]
    pub repository_id: String,
    /// The last validated display name.
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub name: String,
    /// The canonical absolute worktree path.
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub canonical_path: String,
    /// The last successful explicit-open time in UTC Unix milliseconds.
    pub last_opened_at_unix_milliseconds: u64,
    /// The saved full comparison ref, when one exists.
    #[serde(deserialize_with = "deserialize_optional_non_blank")]
    pub preferred_target: Option<String>,
}

/// Represents the bounded ordered repository history.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct RepositoryHistory {
    /// The repository selected for automatic restoration, when one exists.
    #[serde(deserialize_with = "deserialize_optional_repository_id")]
    pub last_repository_id: Option<String>,
    /// Up to twenty recent repositories.
    #[serde(deserialize_with = "deserialize_recent_repositories")]
    pub repositories: Vec<RecentRepository>,
}

fn deserialize_optional_repository_id<'de, D>(deserializer: D) -> Result<Option<String>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = Option::<String>::deserialize(deserializer)?;
    value
        .map(|id| {
            serde_json::from_value::<RepositoryIdValue>(serde_json::Value::String(id))
                .map(|parsed| parsed.0)
                .map_err(serde::de::Error::custom)
        })
        .transpose()
}

#[derive(Deserialize)]
struct RepositoryIdValue(#[serde(deserialize_with = "deserialize_repository_id")] String);

fn deserialize_optional_non_blank<'de, D>(deserializer: D) -> Result<Option<String>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = Option::<String>::deserialize(deserializer)?;
    if value.as_deref().is_some_and(|item| item.trim().is_empty()) {
        return Err(serde::de::Error::custom(
            "the optional value must not be blank",
        ));
    }
    Ok(value)
}

fn deserialize_recent_repositories<'de, D>(
    deserializer: D,
) -> Result<Vec<RecentRepository>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let repositories = Vec::<RecentRepository>::deserialize(deserializer)?;
    if repositories.len() > 20 {
        return Err(serde::de::Error::custom(
            "repository history must contain at most twenty entries",
        ));
    }
    Ok(repositories)
}

#[cfg(test)]
mod tests {
    use super::{RepositoryHistory, RepositoryRestoreResult};

    const LIST_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/repositories-list-recent.result.json"
    ));
    const NONE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/repositories-restore-last.none.result.json"
    ));

    #[test]
    fn parses_shared_repository_history_fixtures() {
        let list: serde_json::Value = serde_json::from_str(LIST_FIXTURE).unwrap();
        let history: RepositoryHistory = serde_json::from_value(list["result"].clone()).unwrap();
        assert_eq!(history.repositories.len(), 1);
        assert_eq!(
            history.last_repository_id.as_deref(),
            Some("01234567-89ab-cdef-0123-456789abcdef")
        );

        let none: serde_json::Value = serde_json::from_str(NONE_FIXTURE).unwrap();
        let restoration: RepositoryRestoreResult =
            serde_json::from_value(none["result"].clone()).unwrap();
        assert_eq!(restoration, RepositoryRestoreResult::None);
    }

    #[test]
    fn rejects_noncanonical_repository_identifiers() {
        let value = r#"{"lastRepositoryId":"0123456789abcdef0123456789abcdef","repositories":[]}"#;
        serde_json::from_str::<RepositoryHistory>(value)
            .expect_err("noncanonical identifiers must be rejected");
    }
}
